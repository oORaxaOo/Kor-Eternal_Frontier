namespace KorTheBlight.Simulation;

internal enum ResidentTrait { Industrious, Resilient, Sociable, Solitary, Curious, Careful, Anxious, Empathetic }
internal enum ResidentDailyActivity { Sleep, Recreation, Work }
internal enum ResidentMemoryKind { HungerCrisis, Exhaustion, AteMeal, Rested, EnvironmentalHazard, MentalBreak, Recovery, FirstMeeting, Friendship, SocialConflict, WorkCompleted, ResourceDelivered, ConstructionCompleted, ProductionCompleted }

internal sealed record ResidentMemory(long Id,long Tick,ResidentMemoryKind Kind,int RelatedResidentId,int MoodImpact,string Detail);

internal sealed class ResidentLifeSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public long NextMemoryId { get; set; } = 1;
    public int ScheduleOffsetHours { get; set; }
    public List<ResidentTrait> Traits { get; set; } = [];
    public List<ResidentMemory> Memories { get; set; } = [];
    public bool IsValid(int residentId)
    {
        if(residentId<=0||SchemaVersion is <1 or >2||NextMemoryId<1||(SchemaVersion>=2&&(ScheduleOffsetHours < -1 || ScheduleOffsetHours > 1))||Traits is null||Traits.Count!=2||
           Traits.Any(t=>!Enum.IsDefined(t))||Traits.Distinct().Count()!=2||
           (Traits.Contains(ResidentTrait.Sociable)&&Traits.Contains(ResidentTrait.Solitary))||
           Memories is null||Memories.Count>ResidentLifeProfile.MaximumMemories) return false;
        long priorTick=-1,priorId=0;
        foreach(var m in Memories)
        {
            if(m is null||m.Id<=priorId||m.Id>=NextMemoryId||m.Tick<priorTick||m.Tick<0||
               !Enum.IsDefined(m.Kind)||m.RelatedResidentId<0||m.MoodImpact is <-12 or >12||
               string.IsNullOrWhiteSpace(m.Detail)||m.Detail.Length>180)return false;
            priorTick=m.Tick;priorId=m.Id;
        }
        return true;
    }
}
internal sealed class ResidentLifeProfile
{
    public const int MaximumMemories=64;
    public const long TicksPerHour=216_000;
    public const long TicksPerDay=24*TicksPerHour;
    private const long MoodMemoryLifetimeTicks=18_000;
    private readonly List<ResidentTrait> _traits=[];
    private readonly List<ResidentMemory> _memories=[];
    private long _nextMemoryId=1;
    private int _scheduleOffsetHours;
    public IReadOnlyList<ResidentTrait> Traits=>_traits;
    public IReadOnlyList<ResidentMemory> Memories=>_memories;
    public string TraitSummary=>string.Join(", ",_traits.Select(Label));
    public string LatestMemorySummary=>_memories.Count==0?"No recorded life events yet.":_memories[^1].Detail;
    public int ScheduleOffsetHours => _scheduleOffsetHours;
    private ResidentLifeProfile(IEnumerable<ResidentTrait> traits, int scheduleOffsetHours = 0)
    {
        _traits.AddRange(traits);
        if (scheduleOffsetHours < -1 || scheduleOffsetHours > 1) throw new ArgumentOutOfRangeException(nameof(scheduleOffsetHours));
        _scheduleOffsetHours = scheduleOffsetHours;
    }
    public ResidentDailyActivity ActivityAtTick(long tick)
    {
        if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
        int hour = (int)((tick % TicksPerDay) / TicksPerHour);
        hour = (hour - _scheduleOffsetHours + 24) % 24;
        if (hour <= 6 || hour >= 22) return ResidentDailyActivity.Sleep;
        if (hour is 8 or 9 or 10 or 11 or 13 or 14 or 15 or 16) return ResidentDailyActivity.Work;
        return ResidentDailyActivity.Recreation;
    }
    public static ResidentLifeProfile Create(int id,string? name=null)
    {
        if(id<=0)throw new ArgumentOutOfRangeException(nameof(id));
        uint state=2166136261u;
        foreach(char ch in name??string.Empty)state=unchecked((state^ch)*16777619u);
        state=unchecked((state^(uint)id)*16777619u);
        var all=Enum.GetValues<ResidentTrait>(); ResidentTrait a,b;
        do{state=unchecked(state*1664525u+1013904223u);a=all[state%(uint)all.Length];
           state=unchecked(state*1664525u+1013904223u);b=all[state%(uint)all.Length];}
        while(a==b||(a==ResidentTrait.Sociable&&b==ResidentTrait.Solitary)||(a==ResidentTrait.Solitary&&b==ResidentTrait.Sociable));
        return new ResidentLifeProfile([a,b], (int)(state % 3) - 1);
    }
    internal static ResidentLifeProfile FromTraits(IEnumerable<ResidentTrait> traits)
    {
        ArgumentNullException.ThrowIfNull(traits);var v=traits.ToArray();
        if(v.Length!=2||v.Any(t=>!Enum.IsDefined(t))||v.Distinct().Count()!=2||
           (v.Contains(ResidentTrait.Sociable)&&v.Contains(ResidentTrait.Solitary)))
            throw new ArgumentException("A resident needs two distinct, compatible personality traits.",nameof(traits));
        return new ResidentLifeProfile(v);
    }
    public bool HasTrait(ResidentTrait t)=>_traits.Contains(t);
    public void RecordMemory(long tick,ResidentMemoryKind kind,int relatedId,int moodImpact,string detail)
    {
        if(tick<0)throw new ArgumentOutOfRangeException(nameof(tick));
        if(!Enum.IsDefined(kind))throw new ArgumentOutOfRangeException(nameof(kind));
        if(relatedId<0)throw new ArgumentOutOfRangeException(nameof(relatedId));
        if(moodImpact is <-12 or >12)throw new ArgumentOutOfRangeException(nameof(moodImpact));
        if(string.IsNullOrWhiteSpace(detail))throw new ArgumentException("A memory needs detail.",nameof(detail));
        detail=detail.Trim();if(detail.Length>180)detail=detail[..177]+"...";
        if(_memories.Count>0){var last=_memories[^1];
            if(last.Tick==tick&&last.Kind==kind&&last.RelatedResidentId==relatedId)return;
            if(tick<last.Tick)throw new InvalidOperationException("Memory ticks must be non-decreasing.");}
        while(_memories.Count>=MaximumMemories)_memories.RemoveAt(0);
        _memories.Add(new ResidentMemory(_nextMemoryId++,tick,kind,relatedId,moodImpact,detail));
    }
    public float RecentMoodBias(long tick)
    {
        if(tick<0)throw new ArgumentOutOfRangeException(nameof(tick));float total=0;
        foreach(var m in _memories){long age=tick-m.Tick;if(age>=0&&age<MoodMemoryLifetimeTicks)total+=m.MoodImpact*(1f-age/(float)MoodMemoryLifetimeTicks);}
        return Math.Clamp(total,-20f,20f);
    }
    public float WorkSpeedMultiplier(long tick,float stress)
    {
        if(tick<0||!float.IsFinite(stress))throw new ArgumentOutOfRangeException(nameof(stress));
        float r=1;if(HasTrait(ResidentTrait.Industrious))r*=1.12f;if(HasTrait(ResidentTrait.Careful))r*=1.03f;
        if(stress>=50&&HasTrait(ResidentTrait.Resilient))r*=1.08f;if(stress>=50&&HasTrait(ResidentTrait.Anxious))r*=.85f;
        return Math.Clamp(r*(1f+Math.Clamp(RecentMoodBias(tick),-10f,10f)*.006f),.65f,1.30f);
    }
    public ResidentLifeSnapshot CaptureSnapshot()=>new(){SchemaVersion=2,NextMemoryId=_nextMemoryId,ScheduleOffsetHours=_scheduleOffsetHours,Traits=_traits.ToList(),Memories=_memories.ToList()};
    public void RestoreSnapshot(ResidentLifeSnapshot s,int id)
    {
        ArgumentNullException.ThrowIfNull(s);if(!s.IsValid(id))throw new InvalidDataException("Invalid resident life data.");
        if (s.SchemaVersion >= 2) _scheduleOffsetHours = s.ScheduleOffsetHours;
        _traits.Clear();_traits.AddRange(s.Traits);_memories.Clear();_memories.AddRange(s.Memories);_nextMemoryId=s.NextMemoryId;
    }
    private static string Label(ResidentTrait t)=>t switch
    {
        ResidentTrait.Industrious=>"Industrious",ResidentTrait.Resilient=>"Resilient",ResidentTrait.Sociable=>"Sociable",
        ResidentTrait.Solitary=>"Solitary",ResidentTrait.Curious=>"Curious",ResidentTrait.Careful=>"Careful",
        ResidentTrait.Anxious=>"Anxious",ResidentTrait.Empathetic=>"Empathetic",_=>t.ToString()
    };
}
