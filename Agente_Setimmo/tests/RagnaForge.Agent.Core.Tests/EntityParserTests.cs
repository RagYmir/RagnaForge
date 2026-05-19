using RagnaForge.Agent.Core.Entities;
using RagnaForge.Agent.Core.Parsing;

namespace RagnaForge.Agent.Core.Tests;

/// <summary>
/// Tests for entity parsers: ItemDbParser, MobDbParser, NpcScriptParser, MapIndexParser.
/// Uses temporary fixture files — never touches real rAthena data.
/// </summary>
public class EntityParserTests : IDisposable
{
    private readonly string _tempDir;

    public EntityParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ragnaforge_parser_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() { try { Directory.Delete(_tempDir, true); } catch { } }

    private string WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    // --- Item Parser ---
    [Fact]
    public void ItemDbParser_ParsesSimpleYaml()
    {
        WriteFile("db/re/item_db.yml", """
Body:
  - Id: 501
    AegisName: Red_Potion
    Name: Red Potion
    Type: Healing
  - Id: 502
    AegisName: Orange_Potion
    Name: Orange Potion
    Type: Healing
""");
        var items = ItemDbParser.ParseAll(_tempDir);
        Assert.Equal(2, items.Count);
        Assert.Equal(501, items[0].Id);
        Assert.Equal("Red_Potion", items[0].AegisName);
        Assert.Equal("Red Potion", items[0].Name);
        Assert.Equal("Healing", items[0].Type);
    }

    [Fact]
    public void ItemDbParser_DetectsMultipleFiles()
    {
        WriteFile("db/re/item_db.yml", "  - Id: 1\n    AegisName: A\n    Name: A");
        WriteFile("db/re/item_db_usable.yml", "  - Id: 2\n    AegisName: B\n    Name: B");
        WriteFile("db/re/item_db_equip.yml", "  - Id: 3\n    AegisName: C\n    Name: C");

        var items = ItemDbParser.ParseAll(_tempDir);
        Assert.Equal(3, items.Count);
    }

    // --- Monster Parser ---
    [Fact]
    public void MobDbParser_ParsesSimpleYaml()
    {
        WriteFile("db/re/mob_db.yml", """
Body:
  - Id: 1002
    AegisName: PORING
    Name: Poring
  - Id: 1001
    AegisName: SCORPION
    Name: Scorpion
""");
        var mobs = MobDbParser.ParseAll(_tempDir);
        Assert.Equal(2, mobs.Count);
        Assert.Contains(mobs, m => m.Id == 1002 && m.AegisName == "PORING");
    }

    // --- NPC Parser ---
    [Fact]
    public void NpcScriptParser_ParsesScriptShopWarp()
    {
        WriteFile("npc/cities/test.txt", """
prontera,155,183,4	script	Kafra Employee	4_F_KAFRA1,{
    mes "Hello";
    close;
}

prontera,120,90,0	shop	Tool Dealer	4_M_MERCHANT,501:-1,502:-1

prontera,156,22,0	warp	prt_south	2,2,prt_fild08,170,375
""");
        var npcs = NpcScriptParser.ParseAll(_tempDir);
        Assert.Equal(3, npcs.Count);
        Assert.Contains(npcs, n => n.Type == "script" && n.Name == "Kafra Employee");
        Assert.Contains(npcs, n => n.Type == "shop" && n.Name == "Tool Dealer");
        Assert.Contains(npcs, n => n.Type == "warp" && n.Name == "prt_south");
    }

    [Fact]
    public void NpcScriptParser_ExtractsMapAndCoords()
    {
        WriteFile("npc/test.txt", "geffen,120,100,4\tscript\tTest NPC\t100,{\n");
        var npcs = NpcScriptParser.ParseAll(_tempDir);
        Assert.Single(npcs);
        Assert.Equal("geffen", npcs[0].Map);
        Assert.Equal(120, npcs[0].X);
        Assert.Equal(100, npcs[0].Y);
        Assert.Equal(4, npcs[0].Direction);
    }

    // --- Map Parser ---
    [Fact]
    public void MapIndexParser_ParsesServerMaps()
    {
        WriteFile("db/map_index.txt", """
// Map Index
// Comment line

prontera 1
alberta
izlude
geffen
""");
        var maps = MapIndexParser.ParseServerMaps(_tempDir);
        Assert.Equal(4, maps.Count);
        Assert.Contains(maps, m => m.Name == "prontera");
        Assert.Contains(maps, m => m.Name == "alberta");
    }

    [Fact]
    public void MapIndexParser_EnrichesWithClientAssets()
    {
        WriteFile("db/map_index.txt", "prontera 1\nalberta\n");
        // Create client files
        WriteFile("patch/data/prontera.rsw", "");
        WriteFile("patch/data/prontera.gnd", "");
        WriteFile("patch/data/prontera.gat", "");

        var maps = MapIndexParser.ParseServerMaps(_tempDir);
        MapIndexParser.EnrichWithClientAssets(maps, Path.Combine(_tempDir, "patch"));

        var prt = maps.First(m => m.Name == "prontera");
        Assert.True(prt.HasRsw);
        Assert.True(prt.HasGnd);
        Assert.True(prt.HasGat);
        Assert.Equal("both", prt.Source);

        var alb = maps.First(m => m.Name == "alberta");
        Assert.False(alb.HasRsw);
        Assert.Equal("server", alb.Source);
    }

    // --- Classification ---
    [Fact]
    public void ItemDbParser_IgnoresCommentedOutEntries()
    {
        WriteFile("db/re/item_db.yml", """
  - Id: 100
    AegisName: Real_Item
    Name: Real Item
#  - Id: 200
#    AegisName: Commented_Out
""");
        var items = ItemDbParser.ParseAll(_tempDir);
        Assert.Single(items);
        Assert.Equal(100, items[0].Id);
    }
}
