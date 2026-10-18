using KorTheBlight.Campaign;
using SkiaSharp;

namespace KorTheBlight.Rendering;

/// <summary>Strategic campaign and menu presentation for the playable campaign slice.</summary>
internal sealed class CampaignRenderer : IDisposable
{
    internal enum Action { None, NewCampaign, Continue, Quit, Develop, Research, March, Battle, EndTurn, Save, Load, Back }
    private static readonly SKColor Ink = new(13, 17, 18), Panel = new(22, 29, 30), Edge = new(75, 83, 78);
    private static readonly SKColor Cream = new(235, 226, 207), Muted = new(174, 180, 172), Red = new(151, 39, 48), Gold = new(218, 177, 91);
    private readonly SKPaint _fill = new() { IsAntialias = true };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
    private readonly SKPaint _text = new() { IsAntialias = true, Color = Cream };
    private readonly SKPaint _subtle = new() { IsAntialias = true, Color = Muted };
    private readonly SKFont _body = new(SKTypeface.FromFamilyName("Noto Sans"), 15);
    private readonly SKFont _small = new(SKTypeface.FromFamilyName("Noto Sans"), 12);
    private readonly SKFont _head = new(SKTypeface.FromFamilyName("Noto Sans", SKFontStyle.Bold), 19);
    private readonly SKFont _title = new(SKTypeface.FromFamilyName("Noto Sans", SKFontStyle.Bold), 40);
    private readonly SKFont _menuLabel = new(SKTypeface.FromFamilyName("Noto Sans", SKFontStyle.Bold), 13);
    private readonly SKFont _regionName = new(SKTypeface.FromFamilyName("Noto Sans", SKFontStyle.Bold), 8);
    private readonly SKFont _micro = new(SKTypeface.FromFamilyName("Noto Sans"), 6);

    public void DrawMenu(SKCanvas c, int seed, bool canContinue, string notice)
    {
        Artwork.Draw(c, "menu-foundry-2560", new SKRect(0, 0, 1100, 720));
        Artwork.Draw(c, "kor-sentinel-tank", new SKRect(260, 248, 650, 527));
        _fill.Color = new SKColor(5, 9, 11, 68); c.DrawRect(new SKRect(0, 0, 1100, 720), _fill);
        // Beveled title plate.
        _fill.Color = new SKColor(8, 12, 14, 225); c.DrawRoundRect(new SKRect(64, 83, 686, 183), 4, 4, _fill);
        _stroke.Color = new SKColor(151, 65, 62, 175); c.DrawRoundRect(new SKRect(64, 83, 686, 183), 4, 4, _stroke);
        _fill.Color = Red; c.DrawRect(new SKRect(64, 83, 70, 183), _fill);
        Center(c, "KOR-ETERNAL FRONTIER", 375, 137, _title, Cream);
        Center(c, "FRONTIER COMMAND  /  CIVILIZATION SURVIVES TOGETHER", 375, 164, _small, Gold);

        Artwork.Draw(c, "kor-hud-metal", new SKRect(724, 90, 1042, 617));
        _fill.Color = new SKColor(7, 10, 12, 175); c.DrawRoundRect(new SKRect(724, 90, 1042, 617), 7, 7, _fill);
        _stroke.Color = new SKColor(99, 91, 75, 160); c.DrawRoundRect(new SKRect(724, 90, 1042, 617), 7, 7, _stroke);
        c.DrawText("COMMAND DECK", 752, 132, SKTextAlign.Left, _head, _text);
        _fill.Color = Red; c.DrawRect(new SKRect(752, 148, 1014, 150), _fill);
        c.DrawText("CAMPAIGN SEED", 752, 182, SKTextAlign.Left, _menuLabel, _text);
        c.DrawText(seed.ToString(), 752, 218, SKTextAlign.Left, _title, _text);
        c.DrawText("UP / DOWN  CHANGE      R  RANDOMIZE", 752, 242, SKTextAlign.Left, _small, _subtle);
        Button(c, 752, 273, 262, 55, "NEW CAMPAIGN", true);
        Button(c, 752, 340, 262, 55, canContinue ? "CONTINUE CAMPAIGN" : "NO CAMPAIGN SAVE", canContinue);
        Button(c, 752, 407, 262, 55, "QUIT TO DESKTOP", true);
        TextColor(c, "KOR FRONTIER DIRECTIVE", 752, 505, _menuLabel, Gold);
        c.DrawText("ALLE FÜR ALLE.", 752, 533, SKTextAlign.Left, _body, _text);
        c.DrawText("EIN KOR LÄSST KEINEN KOR ALLEIN.", 752, 558, SKTextAlign.Left, _small, _subtle);
        TextColor(c, notice, 752, 588, _small, Gold);

        TextColor(c, "THE FRONTIER DOES NOT WAIT", 68, 551, _menuLabel, Gold);
        TextColor(c, "Explore. Build. Hold the line.", 68, 583, _head, Cream);
        TextColor(c, "A persistent campaign above a real-time battlefield.", 68, 612, _body, Muted);
        _stroke.Color = new SKColor(186, 160, 112, 125); c.DrawLine(68, 636, 1037, 636, _stroke);
        c.DrawText("KOR-ETERNAL FRONTIER     •     TURN-BASED CAMPAIGN     /     REAL-TIME BATTLES", 68, 669, SKTextAlign.Left, _small, _subtle);
    }

    public void DrawCampaign(SKCanvas c, CampaignSimulation campaign)
    {
        Artwork.Draw(c, "strategic-map", new SKRect(0, 86, 755, 638));
        Artwork.Draw(c, "kor-hud-metal", new SKRect(0, 0, 1100, 86));
        _fill.Color = new SKColor(7, 11, 13, 115); c.DrawRect(new SKRect(0, 0, 1100, 86), _fill);
        _fill.Color = Red; c.DrawRect(new SKRect(0, 84, 1100, 87), _fill);
        c.DrawText("KOR", 22, 35, SKTextAlign.Left, _head, _text);
        c.DrawText("ETERNAL FRONTIER", 22, 61, SKTextAlign.Left, _small, _subtle);
        DrawResourceCard(c, 250, "SUPPLIES", campaign.State.Supplies, new SKColor(154, 184, 126), "S");
        DrawResourceCard(c, 422, "ALLOY", campaign.State.Alloy, new SKColor(212, 156, 91), "A");
        DrawResourceCard(c, 594, "ENERGY", campaign.State.Energy, new SKColor(221, 190, 91), "E");
        DrawResourceCard(c, 766, "KNOWLEDGE", campaign.State.Knowledge, new SKColor(115, 170, 190), "K");
        _fill.Color = new SKColor(30, 38, 39); c.DrawRoundRect(new SKRect(950, 17, 1076, 69), 5, 5, _fill);
        c.DrawText("CAMPAIGN", 1013, 37, SKTextAlign.Center, _small, _subtle);
        c.DrawText($"TURN {campaign.State.Turn:00}", 1013, 59, SKTextAlign.Center, _head, _text);
        // Hex links, rendered behind the region markers.
        _stroke.Color = new SKColor(178, 173, 139, 65); _stroke.StrokeWidth = 4;
        foreach (var region in campaign.State.Regions)
            foreach (var other in campaign.State.Regions.Where(other => other.Id > region.Id && campaign.IsAdjacent(region.Id, other.Id)))
            { var a = CenterOf(region); var b = CenterOf(other); c.DrawLine(a.X, a.Y, b.X, b.Y, _stroke); }
        _stroke.StrokeWidth = 1;
        foreach (var region in campaign.State.Regions)
        {
            var p = CenterOf(region); bool selected = region.Id == campaign.State.SelectedRegionId;
            SKColor color = region.Owner switch { "Kor" => new SKColor(118, 35, 42), "Blight" => new SKColor(86, 21, 32), _ => new SKColor(72, 83, 66) };
            if (region.Explored)
            {
                string terrainArt = "region-" + region.Terrain.ToLowerInvariant().Replace(' ', '-');
                Artwork.Draw(c, terrainArt, new SKRect(p.X - 42, p.Y - 42, p.X + 42, p.Y + 42));
                DrawHex(c, p.X, p.Y, 41, new SKColor(color.Red, color.Green, color.Blue, 52), null);
                DrawHex(c, p.X + 1, p.Y + 1, 43, SKColors.Transparent, new SKColor(166, 164, 139, 190));
            }
            else
            {
                DrawHex(c, p.X + 3, p.Y + 5, 43, new SKColor(4, 6, 7, 185), null);
                DrawHex(c, p.X, p.Y, 43, new SKColor(30, 36, 35), new SKColor(102, 111, 97, 180));
            }
            if (region.Explored) DrawRegionInsignia(c, region, p.X, p.Y);
            if (selected)
            {
                DrawHex(c, p.X, p.Y, 47, SKColors.Transparent, Gold);
                _stroke.Color = new SKColor(247, 210, 125, 165); _stroke.StrokeWidth = 1; c.DrawLine(p.X - 43, p.Y - 43, p.X - 31, p.Y - 43, _stroke);
                c.DrawLine(p.X + 31, p.Y + 43, p.X + 43, p.Y + 43, _stroke);
            }
            if (!region.Explored) Center(c, "UNKNOWN", p.X, p.Y + 4, _small, Muted);
            else
            {
                DrawRegionLabel(c, region, p.X, p.Y);
            }
            if (region.Id == campaign.State.ArmyRegionId) DrawArmyMarker(c, p.X, p.Y - 26);
            if (region.Id == campaign.PendingArmyDestination) DrawHex(c, p.X, p.Y, 50, SKColors.Transparent, Gold);
            if (region.Blight > 0) DrawBlightMarker(c, p.X + 31, p.Y - 27, region.Blight);
        }
        var chosen = campaign.SelectedRegion;
        Artwork.Draw(c, "kor-hud-metal", new SKRect(755, 100, 1080, 590));
        _fill.Color = new SKColor(22, 29, 30, 215); c.DrawRoundRect(new SKRect(755, 100, 1080, 590), 8, 8, _fill);
        _stroke.Color = Edge; c.DrawRoundRect(new SKRect(755, 100, 1080, 590), 8, 8, _stroke);
        c.DrawText(chosen.Explored ? chosen.Name : "Uncharted territory", 778, 135, SKTextAlign.Left, _head, _text);
        string owner = chosen.Explored ? chosen.Owner : "Unknown";
        Write(c, $"Control: {owner}", 778, 169); Write(c, $"Terrain: {(chosen.Explored ? chosen.Terrain : "Unknown")}", 778, 195);
        Write(c, $"Yield: {(chosen.Explored ? $"{chosen.Production} {chosen.Resource}/turn" : "Unknown")}", 778, 221);
        Write(c, $"Development: {chosen.DevelopmentLevel}", 778, 247);
        Write(c, $"Blight pressure: {(chosen.Explored ? chosen.Blight : 0)}", 778, 273);
        Write(c, $"Army: {campaign.State.ArmyStrength} units at region {campaign.State.ArmyRegionId}", 778, 299);
        Write(c, $"Readiness: {campaign.State.ArmyReadiness}%", 778, 325);
        Button(c, 777, 350, 280, 42, campaign.DevelopButtonLabel, campaign.CanDevelopSelected);
        Button(c, 777, 400, 280, 42, campaign.State.WardProtocolResearched ? "WARD PROTOCOL — COMPLETE" : "RESEARCH WARD (30 K / 20 E)", campaign.CanResearchWard);
        Button(c, 777, 450, 280, 42, campaign.PendingArmyDestination >= 0 ? "MARCH ORDER — END TURN" : "ORDER ARMY TO REGION", campaign.CanMarchSelected);
        Button(c, 777, 500, 280, 42, "ENTER REAL-TIME BATTLE", campaign.CanStartBattle);
        Center(c, campaign.Message, 385, 630, _body, Gold);
        Button(c, 740, 656, 100, 42, "END TURN", true);
        Button(c, 850, 656, 100, 42, "SAVE", true);
        Button(c, 960, 656, 100, 42, "MENU", true);
        _text.Color = Muted; c.DrawText("Click a region to inspect it. Kor army is marked with gold.", 28, 690, SKTextAlign.Left, _small, _text); _text.Color = Cream;
    }

    public static Action HitMenu(float x, float y) => y is >= 273 and <= 328 && x is >= 752 and <= 1014 ? Action.NewCampaign
        : y is >= 340 and <= 395 && x is >= 752 and <= 1014 ? Action.Continue
        : y is >= 407 and <= 462 && x is >= 752 and <= 1014 ? Action.Quit : Action.None;
    public static Action HitCampaign(float x, float y) => y is >= 350 and <= 392 && x is >= 777 and <= 1057 ? Action.Develop
        : y is >= 400 and <= 442 && x is >= 777 and <= 1057 ? Action.Research
        : y is >= 450 and <= 492 && x is >= 777 and <= 1057 ? Action.March
        : y is >= 500 and <= 542 && x is >= 777 and <= 1057 ? Action.Battle
        : y is >= 656 and <= 698 && x is >= 740 and <= 840 ? Action.EndTurn
        : y is >= 656 and <= 698 && x is >= 850 and <= 950 ? Action.Save
        : y is >= 656 and <= 698 && x is >= 960 and <= 1060 ? Action.Back : Action.None;
    public static int? PickRegion(float x, float y, CampaignState state)
    {
        foreach (var region in state.Regions)
        { var p = CenterOf(region); if (MathF.Sqrt(MathF.Pow(x - p.X, 2) + MathF.Pow(y - p.Y, 2)) <= 37) return region.Id; }
        return null;
    }
    private static (float X, float Y) CenterOf(RegionState region) => (375 + (region.Q + region.R * .5f) * 112, 371 + region.R * 82);

    private void DrawMenuSkyline(SKCanvas c)
    {
        _fill.Color = new SKColor(7, 12, 14, 230); c.DrawRect(new SKRect(0, 430, 704, 530), _fill);
        // Foundry blocks: stepped roof lines, cooling stacks, windows, gantries, and red beacon lamps.
        Building(c, 26, 351, 112, 171, new SKColor(18, 25, 27), 5);
        Building(c, 144, 393, 88, 129, new SKColor(13, 21, 24), 3);
        Building(c, 238, 327, 138, 195, new SKColor(18, 25, 26), 6);
        Building(c, 382, 388, 105, 134, new SKColor(13, 20, 22), 4);
        Building(c, 496, 360, 164, 162, new SKColor(16, 23, 25), 5);
        _stroke.Color = new SKColor(139, 74, 62, 115); _stroke.StrokeWidth = 2;
        c.DrawLine(54, 380, 112, 380, _stroke); c.DrawLine(267, 352, 347, 352, _stroke);
        c.DrawLine(514, 385, 633, 385, _stroke); c.DrawLine(438, 407, 474, 407, _stroke);
        _stroke.Color = new SKColor(180, 133, 81, 90); _stroke.StrokeWidth = 1;
        for (int x = 0; x < 5; x++) c.DrawLine(167 + x * 12, 421, 167 + x * 12, 512, _stroke);
        c.DrawLine(156, 422, 220, 422, _stroke); c.DrawLine(249, 386, 364, 386, _stroke);
        c.DrawLine(507, 413, 649, 413, _stroke);
        _fill.Color = new SKColor(207, 63, 49); c.DrawCircle(72, 346, 3, _fill); c.DrawCircle(300, 322, 3, _fill); c.DrawCircle(572, 354, 3, _fill);
        _fill.Color = new SKColor(212, 158, 81, 40); c.DrawRect(new SKRect(0, 517, 700, 530), _fill);
        // Stacks and exhaust caps.
        Stack(c, 82, 350, 19, 84); Stack(c, 284, 322, 21, 77); Stack(c, 548, 350, 18, 71);
        _stroke.Color = new SKColor(152, 97, 68, 70); c.DrawLine(0, 522, 700, 522, _stroke);
    }

    private void Building(SKCanvas c, float x, float y, float w, float h, SKColor color, int bays)
    {
        _fill.Color = new SKColor(5, 8, 10, 200); c.DrawRect(new SKRect(x + 7, y + 7, x + w + 7, y + h), _fill);
        using var roof = new SKPath(); roof.MoveTo(x, y + 12); roof.LineTo(x + 14, y); roof.LineTo(x + w - 9, y); roof.LineTo(x + w, y + 12); roof.LineTo(x + w, y + h); roof.LineTo(x, y + h); roof.Close();
        _fill.Color = color; c.DrawPath(roof, _fill);
        _stroke.Color = new SKColor(114, 127, 120, 100); c.DrawPath(roof, _stroke);
        _fill.Color = new SKColor(5, 10, 12, 180); c.DrawRect(new SKRect(x + 9, y + 28, x + w - 9, y + h - 8), _fill);
        for (int i = 0; i < bays; i++)
        {
            float bx = x + 13 + i * Math.Max(12, (w - 30) / (float)bays);
            _fill.Color = new SKColor(196, 128, 70, (byte)(80 + (i * 41 % 90)));
            c.DrawRect(new SKRect(bx, y + 36, bx + 5, y + 51), _fill);
            c.DrawRect(new SKRect(bx, y + 72, bx + 5, y + 87), _fill);
        }
    }

    private void Stack(SKCanvas c, float x, float y, float w, float h)
    {
        _fill.Color = new SKColor(10, 16, 18); c.DrawRect(new SKRect(x, y, x + w, y + h), _fill);
        _fill.Color = new SKColor(52, 61, 60); c.DrawRect(new SKRect(x + 3, y + 4, x + w - 3, y + h), _fill);
        _fill.Color = new SKColor(140, 44, 44); c.DrawRect(new SKRect(x - 2, y, x + w + 2, y + 6), _fill);
        _stroke.Color = new SKColor(171, 137, 96, 95); c.DrawLine(x + 6, y + 15, x + 6, y + h - 6, _stroke);
    }

    private void DrawMenuDust(SKCanvas c)
    {
        for (int i = 0; i < 96; i++)
        {
            int x = 18 + (i * 197 + i * i * 13) % 675;
            int y = 210 + (i * 83 + i * i * 7) % 315;
            byte alpha = (byte)(22 + (i * 37 % 55));
            _fill.Color = i % 7 == 0 ? new SKColor(232, 126, 78, alpha) : new SKColor(190, 186, 160, alpha);
            c.DrawCircle(x, y, i % 9 == 0 ? 1.6f : .8f, _fill);
        }
    }

    private void DrawMapTexture(SKCanvas c)
    {
        _stroke.Color = new SKColor(177, 163, 129, 20); _stroke.StrokeWidth = 1;
        for (int ring = 0; ring < 8; ring++)
        {
            float inset = ring * 22;
            var path = new SKPath();
            path.MoveTo(20 + inset, 256 - ring * 4); path.CubicTo(160, 125 + inset, 438 - inset, 175, 688 - inset, 230 + inset * .25f);
            path.CubicTo(748 - inset * .5f, 408, 514, 577 - inset, 228 + inset * .2f, 548);
            c.DrawPath(path, _stroke); path.Dispose();
        }
        _stroke.Color = new SKColor(220, 199, 156, 16);
        for (int i = 0; i < 22; i++)
        {
            float x = (i * 139 + 23) % 730, y = 112 + (i * 97) % 495;
            c.DrawLine(x, y, x + 9 + i % 8, y - 3 - i % 5, _stroke);
        }
        _stroke.StrokeWidth = 1;
    }

    private void DrawRegionInsignia(SKCanvas c, RegionState region, float x, float y)
    {
        if (region.Owner == "Blight")
        {
            _stroke.Color = new SKColor(226, 77, 72, 145); _stroke.StrokeWidth = 2;
            c.DrawLine(x, y - 26, x - 6, y - 18, _stroke); c.DrawLine(x - 6, y - 18, x + 5, y - 11, _stroke);
            c.DrawLine(x + 5, y - 11, x + 2, y - 4, _stroke); c.DrawLine(x - 6, y - 18, x - 14, y - 14, _stroke);
            c.DrawLine(x + 5, y - 11, x + 13, y - 17, _stroke); _stroke.StrokeWidth = 1;
            _fill.Color = new SKColor(229, 94, 74, 185); c.DrawCircle(x, y - 26, 2.5f, _fill); c.DrawCircle(x - 14, y - 14, 2, _fill);
            return;
        }
        if (region.Owner == "Kor")
        {
            _fill.Color = new SKColor(218, 173, 96, 150); c.DrawRect(new SKRect(x - 14, y - 25, x + 14, y - 23), _fill);
            _fill.Color = new SKColor(222, 180, 105, 170); c.DrawRect(new SKRect(x - 10, y - 31, x - 7, y - 25), _fill);
            c.DrawRect(new SKRect(x + 7, y - 31, x + 10, y - 25), _fill); return;
        }
        _stroke.Color = new SKColor(211, 189, 133, 120); _stroke.StrokeWidth = 1.5f;
        switch (region.Resource)
        {
            case "Alloy": c.DrawLine(x - 8, y - 19, x, y - 29, _stroke); c.DrawLine(x, y - 29, x + 9, y - 18, _stroke); c.DrawLine(x - 8, y - 19, x + 9, y - 18, _stroke); break;
            case "Energy": c.DrawLine(x + 2, y - 31, x - 5, y - 21, _stroke); c.DrawLine(x - 5, y - 21, x + 1, y - 21, _stroke); c.DrawLine(x + 1, y - 21, x - 2, y - 12, _stroke); break;
            case "Knowledge": c.DrawCircle(x, y - 22, 7, _stroke); c.DrawLine(x, y - 30, x, y - 14, _stroke); break;
            default: c.DrawLine(x - 10, y - 17, x, y - 26, _stroke); c.DrawLine(x, y - 26, x + 10, y - 17, _stroke); c.DrawLine(x - 10, y - 17, x + 10, y - 17, _stroke); break;
        }
        _stroke.StrokeWidth = 1;
    }

    private void DrawArmyMarker(SKCanvas c, float x, float y)
    {
        Artwork.Draw(c, "kor-sentinel-tank", new SKRect(x - 27, y - 22, x + 27, y + 18));
    }

    private void DrawBlightMarker(SKCanvas c, float x, float y, int level)
    {
        _fill.Color = new SKColor(47, 10, 20, 230); c.DrawCircle(x, y, 10, _fill);
        _stroke.Color = new SKColor(219, 70, 65); c.DrawCircle(x, y, 9, _stroke);
        Center(c, level.ToString(), x, y + 4, _regionName, new SKColor(255, 194, 155));
    }

    private void DrawRegionLabel(SKCanvas c, RegionState region, float x, float y)
    {
        string[] words = region.Name.ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            Center(c, string.Join(' ', words[..^1]), x, y + 1, _regionName, Cream);
            Center(c, words[^1], x, y + 11, _regionName, Cream);
        }
        else Center(c, region.Name.ToUpperInvariant(), x, y + 5, _regionName, Cream);
        Center(c, $"+{region.Production} {ResourceCode(region.Resource)}", x, y + 27, _micro, Muted);
    }

    private static string ResourceCode(string resource) => resource switch
    { "Supplies" => "SUP", "Alloy" => "ALY", "Energy" => "NRG", "Knowledge" => "KNW", _ => "?" };

    private void DrawResourceCard(SKCanvas c, float x, string label, int amount, SKColor color, string icon)
    {
        _fill.Color = new SKColor(22, 29, 31); c.DrawRoundRect(new SKRect(x, 15, x + 160, 70), 4, 4, _fill);
        _stroke.Color = new SKColor(78, 89, 87); c.DrawRoundRect(new SKRect(x, 15, x + 160, 70), 4, 4, _stroke);
        _fill.Color = color; c.DrawCircle(x + 24, 42, 15, _fill);
        _fill.Color = new SKColor(9, 14, 15); c.DrawCircle(x + 24, 42, 12, _fill);
        Center(c, icon, x + 24, 47, _menuLabel, color);
        c.DrawText(label, x + 48, 35, SKTextAlign.Left, _small, _subtle);
        c.DrawText(amount.ToString("N0"), x + 48, 58, SKTextAlign.Left, _head, _text);
    }

    private void DrawHex(SKCanvas c, float x, float y, float radius, SKColor fill, SKColor? stroke)
    {
        using var path = new SKPath();
        for (int i = 0; i < 6; i++)
        {
            float angle = (-90 + 60 * i) * MathF.PI / 180f;
            float px = x + MathF.Cos(angle) * radius, py = y + MathF.Sin(angle) * radius;
            if (i == 0) path.MoveTo(px, py); else path.LineTo(px, py);
        }
        path.Close();
        if (fill != SKColors.Transparent) { _fill.Color = fill; c.DrawPath(path, _fill); }
        if (stroke.HasValue) { _stroke.Color = stroke.Value; _stroke.StrokeWidth = 1.2f; c.DrawPath(path, _stroke); _stroke.StrokeWidth = 1; }
    }

    private static SKColor Lerp(SKColor a, SKColor b, float amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return new SKColor((byte)(a.Red + (b.Red - a.Red) * amount), (byte)(a.Green + (b.Green - a.Green) * amount),
            (byte)(a.Blue + (b.Blue - a.Blue) * amount), (byte)(a.Alpha + (b.Alpha - a.Alpha) * amount));
    }
    private void Button(SKCanvas c, float x, float y, float w, float h, string label, bool enabled)
    {
        Artwork.Draw(c, enabled ? "kor-button-red" : "kor-button-steel", new SKRect(x, y, x + w, y + h));
        if (!enabled) { _fill.Color = new SKColor(6, 10, 11, 95); c.DrawRect(new SKRect(x, y, x + w, y + h), _fill); }
        Center(c, label, x + w / 2, y + h / 2 + 5, _menuLabel, enabled ? Cream : Muted);
    }
    private void Write(SKCanvas c, string text, float x, float y) => c.DrawText(text, x, y, SKTextAlign.Left, _body, _text);
    private void TextColor(SKCanvas c, string text, float x, float y, SKFont font, SKColor color)
    { _text.Color = color; c.DrawText(text, x, y, SKTextAlign.Left, font, _text); _text.Color = Cream; }
    private void Center(SKCanvas c, string value, float x, float y, SKFont font, SKColor color)
    { _text.Color = color; c.DrawText(value, x, y, SKTextAlign.Center, font, _text); _text.Color = Cream; }
    public void Dispose() { _fill.Dispose(); _stroke.Dispose(); _text.Dispose(); _subtle.Dispose(); _body.Dispose(); _small.Dispose(); _head.Dispose(); _title.Dispose(); _menuLabel.Dispose(); _regionName.Dispose(); _micro.Dispose(); }
}
