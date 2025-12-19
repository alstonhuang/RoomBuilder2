using System;
using System.Collections.Generic;
using System.Linq;
using MyGame.Core;
using NUnit.Framework;

public class BlueprintSpecsTests
{
    private sealed class TestLogger : ILogger
    {
        public readonly List<string> Messages = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public void Log(string message) => Messages.Add(message);
        public void LogWarning(string message) => Warnings.Add(message);
        public void LogError(string message) => Errors.Add(message);
    }

    private sealed class TestLibrary : IItemLibrary
    {
        private readonly Dictionary<string, SimpleVector3> _sizes = new Dictionary<string, SimpleVector3>(StringComparer.Ordinal);
        private readonly Dictionary<string, SimpleVector3> _minBounds = new Dictionary<string, SimpleVector3>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<CoreGenerationRule>> _rules = new Dictionary<string, List<CoreGenerationRule>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _themes = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _tags = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        public TestLibrary WithItem(string id, SimpleVector3 size, SimpleVector3? minBounds = null)
        {
            _sizes[id] = size;
            _minBounds[id] = minBounds ?? new SimpleVector3(0, 0, 0);
            if (!_rules.ContainsKey(id)) _rules[id] = new List<CoreGenerationRule>();
            return this;
        }

        public TestLibrary WithTheme(string themeId, params string[] items)
        {
            _themes[themeId] = items?.ToList() ?? new List<string>();
            return this;
        }

        public TestLibrary WithTag(string tag, params string[] ids)
        {
            _tags[tag] = ids?.ToList() ?? new List<string>();
            return this;
        }

        public TestLibrary WithRules(string itemId, params CoreGenerationRule[] rules)
        {
            _rules[itemId] = rules?.ToList() ?? new List<CoreGenerationRule>();
            return this;
        }

        public SimpleVector3 GetMinBounds(string itemID) => _minBounds.TryGetValue(itemID, out var v) ? v : new SimpleVector3(0, 0, 0);
        public List<CoreGenerationRule> GetRules(string itemID) => _rules.TryGetValue(itemID, out var v) ? v : new List<CoreGenerationRule>();
        public SimpleVector3 GetItemSize(string itemID) => _sizes.TryGetValue(itemID, out var v) ? v : new SimpleVector3(1, 1, 1);

        public string GetRandomItemIDByTag(string tag)
        {
            if (!_tags.TryGetValue(tag, out var list) || list.Count == 0) return null;
            return list[0];
        }

        public List<string> GetItemsInTheme(string themeID) => _themes.TryGetValue(themeID, out var v) ? v : new List<string>();
    }

    [Test]
    public void BP1_GenerateFromTheme_ProducesFloorWallsAndRuleChildren_WithValidParentGraph()
    {
        var logger = new TestLogger();
        var library = new TestLibrary()
            .WithItem("FloorTile", new SimpleVector3(1f, 0.2f, 1f))
            .WithItem("Wall", new SimpleVector3(1f, 3f, 0.2f))
            .WithItem("Table", new SimpleVector3(1.2f, 1f, 1.2f))
            .WithItem("Cup", new SimpleVector3(0.3f, 0.3f, 0.3f))
            .WithTheme("TestTheme", "Table")
            .WithRules("Table", new CoreGenerationRule
            {
                useTag = false,
                targetIDorTag = "Cup",
                type = CorePlacementType.Fixed,
                useDensity = false,
                minCount = 1,
                maxCount = 1,
                probability = 1f,
                offset = new SimpleVector3(0, 0, 0)
            });

        var gen = new RoomGenerator(logger, library);
        var bounds = new SimpleBounds(new SimpleVector3(0, 1f, 0), new SimpleVector3(10f, 2f, 10f));
        var bp = gen.GenerateFromTheme(bounds, "TestTheme", false, false, false, false);

        Assert.That(logger.Errors, Is.Empty, "Generator should not log errors");
        Assert.That(bp, Is.Not.Null);
        Assert.That(bp.nodes, Is.Not.Null);
        Assert.That(bp.nodes.Count, Is.GreaterThan(0));

        Assert.That(bp.nodes.Any(n => n.itemID == "FloorTile"), "Blueprint should contain FloorTile nodes");
        Assert.That(bp.nodes.Any(n => n.itemID == "Wall"), "Blueprint should contain Wall nodes");
        Assert.That(bp.nodes.Any(n => n.itemID == "Table"), "Blueprint should contain Table nodes");
        Assert.That(bp.nodes.Any(n => n.itemID == "Cup"), "Blueprint should contain Cup nodes from rules");

        // instanceID unique
        var ids = bp.nodes.Select(n => n.instanceID).ToList();
        Assert.That(ids.Count, Is.EqualTo(ids.Distinct().Count()), "instanceID must be unique across nodes");

        // parent refs valid + acyclic
        var nodeById = bp.nodes.ToDictionary(n => n.instanceID, n => n);
        foreach (var n in bp.nodes)
        {
            if (string.IsNullOrEmpty(n.parentID)) continue;
            Assert.That(nodeById.ContainsKey(n.parentID), $"parentID must exist: {n.instanceID} -> {n.parentID}");
        }

        foreach (var n in bp.nodes)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string cur = n.parentID;
            while (!string.IsNullOrEmpty(cur))
            {
                Assert.That(seen.Add(cur), $"Cycle detected following parents from {n.instanceID}");
                cur = nodeById[cur].parentID;
            }
        }

        // wall facing/rotation invariant
        foreach (var w in bp.nodes.Where(n => n.containerKind == ContainerKind.Wall))
        {
            float y = w.rotation.y;
            switch (w.facing)
            {
                case Facing.South: Assert.That(y, Is.EqualTo(0f)); break;
                case Facing.West: Assert.That(y, Is.EqualTo(90f)); break;
                case Facing.North: Assert.That(y, Is.EqualTo(180f)); break;
                case Facing.East: Assert.That(y, Is.EqualTo(270f)); break;
            }
        }

        // fixed/local-offset children must have empty logical bounds to preserve "local offset" semantics
        foreach (var c in bp.nodes.Where(n => !string.IsNullOrEmpty(n.parentID) && n.itemID == "Cup"))
        {
            Assert.That(c.logicalBounds.size.x, Is.EqualTo(0f));
            Assert.That(c.logicalBounds.size.y, Is.EqualTo(0f));
            Assert.That(c.logicalBounds.size.z, Is.EqualTo(0f));
        }

        // container tree exists (single-room baseline)
        Assert.That(bp.containers, Is.Not.Null);
        Assert.That(bp.containers.Count, Is.EqualTo(1));
        var root = bp.containers[0];
        var childIds = root.children.Select(x => x.instanceID).ToList();
        Assert.That(childIds, Does.Contain("Group_Floor"));
        Assert.That(childIds, Does.Contain("Group_Wall"));
        Assert.That(childIds, Does.Contain("Group_Furniture"));
    }
}

