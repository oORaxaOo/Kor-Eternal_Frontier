using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KorEternalFrontier
{
    public enum KorKind { Pioneer, Guardian, Blight, CommandCore, Solar, Extractor, Archive, Barracks, Turret, Relay }

    public sealed class KorEntity : MonoBehaviour
    {
        public KorGame World;
        public KorKind Kind;
        public int Team;
        public int HitPoints = 100;
        public bool Building;
        public Vector3 Destination;
        public float Cooldown;
        public bool IsConstructing;
        public float BuildWorkRequired, BuildWorkDone;
        Renderer body;
        public string Label => Kind.ToString();

        void Awake() { body = GetComponent<Renderer>(); }
        void Update()
        {
            if (World == null || World.Paused) return;
            if (IsConstructing)
            {
                int workers = 0;
                foreach (var worker in World.EntitiesSnapshot())
                    if (worker != null && worker.Team == 0 && worker.Kind == KorKind.Pioneer &&
                        Vector3.Distance(worker.transform.position, transform.position) <= 7f) workers++;
                if (workers > 0)
                {
                    BuildWorkDone += Time.deltaTime * workers;
                    if (BuildWorkDone >= BuildWorkRequired)
                    {
                        IsConstructing = false;
                        World.Notice = Kind + " construction complete.";
                    }
                }
                return;
            }
            if (!Building && Kind != KorKind.Blight)
                transform.position = Vector3.MoveTowards(transform.position, Destination, (Kind == KorKind.Guardian ? 4.2f : 3f) * Time.deltaTime);
            Cooldown -= Time.deltaTime;
            if (Kind == KorKind.Blight || Kind == KorKind.Guardian || Kind == KorKind.Turret || Kind == KorKind.Barracks)
            {
                var target = World.FindEnemy(this, Kind == KorKind.Turret ? 8f : 5f);
                if (target != null && Cooldown <= 0f)
                {
                    int damage = Kind == KorKind.Turret ? 28 : (Kind == KorKind.Blight ? 8 : 14);
                    if (Team == 0 && World.HasResearch("Blight Countermeasures"))
                        damage += Kind == KorKind.Turret ? 14 : (Kind == KorKind.Guardian ? 6 : 0);
                    target.Damage(damage);
                    Cooldown = Kind == KorKind.Turret ? 1.1f : 1.35f;
                }
            }
            if (Kind == KorKind.Blight && UnityEngine.Random.value < Time.deltaTime * .035f) World.TrySpread(transform.position);
        }
        public void Damage(int amount)
        {
            HitPoints -= amount;
            if (HitPoints <= 0) { World.Remove(this); Destroy(gameObject); }
        }
        public void Tint(bool selected)
        {
            if (body != null) body.material.color = selected ? new Color(1f, .78f, .28f) : World.ColorFor(Kind, Team);
        }
    }

    [Serializable]
    public sealed class KorEntitySave
    {
        public int Kind, Team, HitPoints;
        public bool Building, IsConstructing;
        public float BuildWorkRequired, BuildWorkDone;
        public Vector3 Position, Destination;
    }

    [Serializable]
    public sealed class KorSaveData
    {
        public int Version = 1, Seed, MapSize, Supplies, Alloy, Energy, Knowledge, Era;
        public KorEntitySave[] Entities;
        public string[] Research;
    }

    public sealed class KorGame : MonoBehaviour
    {
        public int Seed = 73421, MapSize = 52;
        public int Supplies = 420, Alloy = 220, Energy = 100, Knowledge;
        public int Era = 1;
        public bool Paused;
        public string Notice = "The frontier is waiting, Kor.";
        readonly List<KorEntity> entities = new List<KorEntity>();
        public List<KorEntity> EntitiesSnapshot() => new List<KorEntity>(entities);
        readonly List<KorEntity> selected = new List<KorEntity>();
        readonly HashSet<string> researched = new HashSet<string>();
        System.Random rng;
        Camera cameraRig;
        string buildMode = "";
        float economyTimer, blightTimer;

        void Start()
        {
            rng = new System.Random(Seed);
            Application.targetFrameRate = 60;
            BuildTerrain();
            SetupCamera();
            Spawn(KorKind.CommandCore, new Vector3(0, .8f, 0), true, 0);
            Spawn(KorKind.Pioneer, new Vector3(2, .55f, 0), false, 0);
            Spawn(KorKind.Pioneer, new Vector3(-2, .55f, 0), false, 0);
            Spawn(KorKind.Guardian, new Vector3(0, .55f, 2), false, 0);
            for (int i = 0; i < 8; i++)
                Spawn(KorKind.Blight, new Vector3(Range(-MapSize / 2, MapSize / 2) * 1.5f, .6f, Range(-MapSize / 2, MapSize / 2) * 1.5f), false, 1);
        }

        int Range(int min, int max) => rng.Next(min, max);
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5)) SaveGame();
            if (Input.GetKeyDown(KeyCode.F9)) LoadGame();
            if (Input.GetKeyDown(KeyCode.Space)) Paused = !Paused;
            if (Input.GetKeyDown(KeyCode.Escape)) buildMode = "";
            if (Input.GetKeyDown(KeyCode.B)) { buildMode = "Solar"; Notice = "Place a Solar Collector with left-click."; }
            MoveCamera();
            Orders();
            if (Paused) return;
            economyTimer += Time.deltaTime; blightTimer += Time.deltaTime;
            if (economyTimer >= 2f) { economyTimer = 0; Economy(); }
            if (blightTimer >= 9f) { blightTimer = 0; ReseedBlight(); }
        }

        void BuildTerrain()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground / Click Surface";
            ground.transform.position = new Vector3(0, -.52f, 0);
            ground.transform.localScale = Vector3.one * (MapSize * 1.5f / 10f);
            ground.GetComponent<Renderer>().enabled = false;
            var mats = new[] { Mat(new Color(.15f,.21f,.18f)), Mat(new Color(.20f,.25f,.20f)), Mat(new Color(.25f,.19f,.15f)) };
            for (int x = -MapSize/2; x < MapSize/2; x++)
            for (int z = -MapSize/2; z < MapSize/2; z++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "Terrain";
                tile.transform.position = new Vector3(x*1.5f, -.28f, z*1.5f);
                tile.transform.localScale = new Vector3(1.46f,.45f,1.46f);
                tile.GetComponent<Renderer>().sharedMaterial = mats[rng.NextDouble() < .09 ? 2 : 0];
                Destroy(tile.GetComponent<Collider>());
                if (rng.NextDouble() < .012 && (Mathf.Abs(x) > 5 || Mathf.Abs(z) > 5))
                {
                    var deposit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    deposit.name = "Resource Deposit";
                    deposit.transform.position = new Vector3(x*1.5f,-.01f,z*1.5f);
                    deposit.transform.localScale = new Vector3(.45f,.25f,.45f);
                    deposit.GetComponent<Renderer>().material = Mat(new Color(.78f,.52f,.18f));
                    Destroy(deposit.GetComponent<Collider>());
                }
            }
        }

        void SetupCamera()
        {
            cameraRig = Camera.main;
            if (cameraRig == null) { var go = new GameObject("Main Camera"); cameraRig = go.AddComponent<Camera>(); go.tag = "MainCamera"; }
            cameraRig.transform.position = new Vector3(0, 48, -42);
            cameraRig.transform.rotation = Quaternion.Euler(48, 0, 0);
            cameraRig.fieldOfView = 55;
            if (FindObjectOfType<Light>() == null)
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.25f; sun.transform.rotation = Quaternion.Euler(50,-30,0);
            }
            RenderSettings.ambientLight = new Color(.42f,.41f,.40f);
        }

        void MoveCamera()
        {
            if (cameraRig == null) return;
            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"));
            cameraRig.transform.position += Quaternion.Euler(0,cameraRig.transform.eulerAngles.y,0) * input * Time.deltaTime * (Input.GetKey(KeyCode.LeftShift) ? 45f : 24f);
            cameraRig.transform.position += cameraRig.transform.forward * Input.mouseScrollDelta.y * 3f;
            var p = cameraRig.transform.position;
            cameraRig.transform.position = new Vector3(Mathf.Clamp(p.x,-MapSize,MapSize),Mathf.Clamp(p.y,18,90),Mathf.Clamp(p.z,-MapSize,MapSize));
        }

        void Orders()
        {
            if (cameraRig == null) return;
            Vector2 guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (guiMouse.x > Screen.width - 262 || guiMouse.y < 70 || guiMouse.y > Screen.height - 66) return;
            if (Input.GetMouseButtonDown(0))
            {
                var ray = cameraRig.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 300f))
                {
                    var e = hit.collider.GetComponentInParent<KorEntity>();
                    if (e != null && e.Team == 0) { Select(e); return; }
                    if (buildMode != "" && selected.Exists(s => s.Kind == KorKind.Pioneer)) { Build(buildMode, hit.point); return; }
                    Notice = "Right-click to issue a movement or attack order.";
                }
            }
            if (Input.GetMouseButtonDown(1) && selected.Count > 0)
            {
                var ray = cameraRig.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 300f))
                {
                    var target = hit.collider.GetComponentInParent<KorEntity>();
                    foreach (var e in selected) if (!e.Building) e.Destination = target != null && target.Team == 1 ? target.transform.position : hit.point;
                    Notice = target != null && target.Team == 1 ? "Engagement order issued." : "Movement order issued.";
                }
            }
        }

        void Select(KorEntity e)
        {
            foreach (var old in selected) old.Tint(false);
            selected.Clear(); selected.Add(e); e.Tint(true);
            Notice = "Selected: " + e.Label;
        }

        void Build(string kind, Vector3 p)
        {
            KorKind k; int s, a, energy;
            switch (kind)
            {
                case "Solar": k=KorKind.Solar; s=55; a=12; energy=0; break;
                case "Extractor": k=KorKind.Extractor; s=70; a=28; energy=0; break;
                case "Archive": k=KorKind.Archive; s=65; a=25; energy=8; break;
                case "Barracks": k=KorKind.Barracks; s=95; a=50; energy=10; break;
                case "Turret": k=KorKind.Turret; s=60; a=38; energy=12; break;
                default: k=KorKind.Relay; s=45; a=22; energy=6; break;
            }
            if (Supplies<s || Alloy<a || Energy<energy) { Notice = "Insufficient resources."; return; }
            Supplies-=s; Alloy-=a; Energy-=energy; p.y=.8f;
            var site = Spawn(k,p,true,0); site.HitPoints=160;
            site.IsConstructing = true;
            site.BuildWorkRequired = k == KorKind.CommandCore ? 0 : (k == KorKind.Barracks ? 14f : (k == KorKind.Archive || k == KorKind.Turret ? 10f : 7f));
            site.BuildWorkDone = 0f;
            buildMode=""; Notice = kind + " construction site placed. Keep a Pioneer nearby to build it.";
        }

        KorEntity Spawn(KorKind kind, Vector3 p, bool building, int team)
        {
            var shape = building ? PrimitiveType.Cube : (kind == KorKind.Blight ? PrimitiveType.Sphere : PrimitiveType.Capsule);
            var go = GameObject.CreatePrimitive(shape); go.name = kind.ToString(); go.transform.position = p;
            go.transform.localScale = building ? (kind == KorKind.CommandCore ? new Vector3(2.4f,1.6f,2.4f) : new Vector3(1.5f,1.2f,1.5f)) : (kind == KorKind.Blight ? Vector3.one*1.1f : new Vector3(.65f,1.1f,.65f));
            var e = go.AddComponent<KorEntity>(); e.World=this; e.Kind=kind; e.Team=team; e.Building=building; e.Destination=p;
            go.GetComponent<Renderer>().material = Mat(ColorFor(kind,team)); entities.Add(e); return e;
        }

        public Color ColorFor(KorKind k, int team)
        {
            if (team == 1) return new Color(.68f,.06f,.15f);
            switch(k)
            {
                case KorKind.CommandCore: return new Color(.09f,.09f,.11f);
                case KorKind.Pioneer: return new Color(.72f,.67f,.55f);
                case KorKind.Guardian: case KorKind.Barracks: return new Color(.62f,.10f,.17f);
                case KorKind.Solar: case KorKind.Relay: return new Color(.80f,.56f,.18f);
                case KorKind.Archive: return new Color(.31f,.22f,.50f);
                case KorKind.Turret: return new Color(.83f,.18f,.15f);
                default: return new Color(.45f,.47f,.48f);
            }
        }

        public bool HasResearch(string name) => researched.Contains(name);

        public KorEntity FindEnemy(KorEntity source, float range)
        {
            KorEntity best=null; float closest=range;
            foreach(var e in entities) if(e!=null && e.Team!=source.Team)
            { float d=Vector3.Distance(e.transform.position,source.transform.position); if(d<closest){closest=d;best=e;} }
            return best;
        }

        public void Remove(KorEntity e)
        {
            entities.Remove(e);
            if (e.Team==1) { Knowledge+=4; Notice="Blight entity neutralised. +4 Knowledge."; }
            else if (e.Kind==KorKind.CommandCore) Notice="Command core lost. The frontier has fallen.";
        }

        public void TrySpread(Vector3 origin)
        {
            int count=0; foreach(var e in entities) if(e!=null && e.Kind==KorKind.Blight) count++;
            if(count>32) return;
            Vector2 d=UnityEngine.Random.insideUnitCircle*7f;
            Spawn(KorKind.Blight,new Vector3(origin.x+d.x,.6f,origin.z+d.y),false,1);
        }

        void ReSeedBlight()
        {
            int count=0; foreach(var e in entities) if(e!=null && e.Kind==KorKind.Blight) count++;
            if(count<3 && Knowledge>20)
            { Spawn(KorKind.Blight,new Vector3(Range(-MapSize,MapSize),.6f,Range(-MapSize,MapSize)),false,1); Notice="Sensors report a new Blight signature."; }
        }

        void Economy()
        {
            Supplies+=researched.Contains("Field Logistics")?6:3;
            Energy+=researched.Contains("Grid Architecture")?5:2;
            foreach(var e in new List<KorEntity>(entities)) if(e!=null && e.Team==0)
            {
                if(e.Kind==KorKind.Solar) Energy+=3;
                if(e.Kind==KorKind.Extractor) Alloy+=4;
                if(e.Kind==KorKind.Archive) Knowledge+=2;
                if(e.Kind==KorKind.Barracks && UnityEngine.Random.value<.15f && Supplies>=30 && Alloy>=12)
                { Supplies-=30; Alloy-=12; Spawn(KorKind.Guardian,e.transform.position+new Vector3(2,0,2),false,0); }
            }
        }

        void ResearchTech(string name,int cost)
        {
            if(researched.Contains(name)){Notice=name+" already researched.";return;}
            if(Knowledge<cost){Notice="Insufficient Knowledge.";return;}
            Knowledge-=cost; researched.Add(name);
            if(name=="Dynastic Synthesis") Era=2;
            Notice="Research complete: "+name;
        }


        string SavePath => Path.Combine(Application.persistentDataPath, "kor-frontier-save.json");

        void SaveGame()
        {
            try
            {
                var data = new KorSaveData
                {
                    Seed = Seed, MapSize = MapSize, Supplies = Supplies, Alloy = Alloy,
                    Energy = Energy, Knowledge = Knowledge, Era = Era,
                    Entities = entities.FindAll(e => e != null).ConvertAll(e => new KorEntitySave
                    {
                        Kind = (int)e.Kind, Team = e.Team, HitPoints = e.HitPoints,
                        Building = e.Building, IsConstructing = e.IsConstructing, BuildWorkRequired = e.BuildWorkRequired, BuildWorkDone = e.BuildWorkDone, Position = e.transform.position, Destination = e.Destination
                    }).ToArray(),
                    Research = new List<string>(researched).ToArray()
                };
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
                Notice = "Frontier saved successfully.";
            }
            catch (Exception ex) { Notice = "Save failed: " + ex.Message; }
        }

        void LoadGame()
        {
            try
            {
                if (!File.Exists(SavePath)) { Notice = "No save found yet. Press F5 to create one."; return; }
                var data = JsonUtility.FromJson<KorSaveData>(File.ReadAllText(SavePath));
                if (data == null || data.Version != 1 || data.Entities == null) { Notice = "Save file is invalid or incompatible."; return; }
                foreach (var old in selected) if (old != null) old.Tint(false);
                selected.Clear();
                foreach (var old in new List<KorEntity>(entities)) if (old != null) Destroy(old.gameObject);
                entities.Clear();
                Seed = data.Seed; MapSize = data.MapSize;
                Supplies = data.Supplies; Alloy = data.Alloy; Energy = data.Energy;
                Knowledge = data.Knowledge; Era = data.Era;
                researched.Clear();
                if (data.Research != null) foreach (var tech in data.Research) if (!string.IsNullOrEmpty(tech)) researched.Add(tech);
                foreach (var saved in data.Entities)
                {
                    if (saved == null || saved.Kind < 0 || saved.Kind > (int)KorKind.Relay) continue;
                    var e = Spawn((KorKind)saved.Kind, saved.Position, saved.Building, saved.Team);
                    e.HitPoints = saved.HitPoints;
                    e.Destination = saved.Destination;
                    e.IsConstructing = saved.IsConstructing;
                    e.BuildWorkRequired = saved.BuildWorkRequired;
                    e.BuildWorkDone = saved.BuildWorkDone;
                }
                buildMode = "";
                Notice = "Frontier loaded successfully.";
            }
            catch (Exception ex) { Notice = "Load failed: " + ex.Message; }
        }

        void OnGUI()
        {
            GUI.skin.label.fontSize=15; GUI.skin.button.fontSize=13;
            GUI.color=new Color(.055f,.05f,.06f,.95f); GUI.Box(new Rect(12,12,Screen.width-24,54),GUIContent.none); GUI.color=Color.white;
            GUI.Label(new Rect(26,22,235,30),"KOR  /  ETERNAL FRONTIER");
            GUI.Label(new Rect(265,22,130,30),"SUPPLIES  "+Supplies);
            GUI.Label(new Rect(400,22,115,30),"ALLOY  "+Alloy);
            GUI.Label(new Rect(520,22,110,30),"ENERGY  "+Energy);
            GUI.Label(new Rect(635,22,155,30),"KNOWLEDGE  "+Knowledge);
            GUI.Label(new Rect(800,22,90,30),"ERA  "+Era);
            if (GUI.Button(new Rect(Screen.width-178,21,72,30),"SAVE [F5]")) SaveGame();
            if (GUI.Button(new Rect(Screen.width-100,21,72,30),"LOAD [F9]")) LoadGame();
            float x=Screen.width-258;
            GUI.color=new Color(.055f,.05f,.06f,.96f); GUI.Box(new Rect(x,80,246,Screen.height-100),GUIContent.none); GUI.color=Color.white;
            GUI.Label(new Rect(x+12,94,220,24),"COMMAND / CONSTRUCTION");
            Btn(x,126,"Solar Collector  [B]",()=>{buildMode="Solar";Notice="Place Solar Collector.";});
            Btn(x,160,"Ore Extractor",()=>{buildMode="Extractor";Notice="Place Ore Extractor.";});
            Btn(x,194,"Knowledge Archive",()=>{buildMode="Archive";Notice="Place Knowledge Archive.";});
            Btn(x,228,"Guardian Barracks",()=>{buildMode="Barracks";Notice="Place Guardian Barracks.";});
            Btn(x,262,"Defence Turret",()=>{buildMode="Turret";Notice="Place Defence Turret.";});
            Btn(x,296,"Relay Beacon",()=>{buildMode="Relay";Notice="Place Relay Beacon.";});
            GUI.Label(new Rect(x+12,337,220,24),"RESEARCH DIRECTIVES");
            Btn(x,366,"Grid Architecture (30 K)",()=>ResearchTech("Grid Architecture",30));
            Btn(x,400,"Field Logistics (25 K)",()=>ResearchTech("Field Logistics",25));
            Btn(x,434,"Blight Countermeasures (40 K)",()=>ResearchTech("Blight Countermeasures",40));
            Btn(x,468,"Dynastic Synthesis (65 K)",()=>ResearchTech("Dynastic Synthesis",65));
            GUI.Label(new Rect(x+12,514,220,75),"WASD: camera  |  Wheel: zoom\nLeft-click: select / build\nRight-click: move / attack\nB: Solar  |  Space: pause");
            GUI.color=new Color(.055f,.05f,.06f,.95f); GUI.Box(new Rect(12,Screen.height-62,Screen.width-290,50),GUIContent.none); GUI.color=Color.white;
            GUI.Label(new Rect(26,Screen.height-52,Screen.width-320,30),(Paused?"PAUSED / ":"")+Notice);
        }

        void Btn(float x,float y,string label,Action action){if(GUI.Button(new Rect(x+10,y,224,29),label))action();}
        Material Mat(Color color)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null) shader=Shader.Find("Standard");
            var m=new Material(shader); m.color=color; return m;
        }
    }
}
