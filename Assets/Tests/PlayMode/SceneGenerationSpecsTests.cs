using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MyGame.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MyGame.Adapters.Unity;

public class SceneGenerationSpecsTests
{
    private const float SnapEpsilon = 0.01f; // ε_snap
    private const float CloseAngleEpsilonDeg = 1f; // ε_closeAngle

    private readonly List<GameObject> _cleanup = new List<GameObject>();

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var go in _cleanup)
        {
            if (go != null) Object.Destroy(go);
        }
        _cleanup.Clear();
        yield return null;
    }

    private GameObject Track(GameObject go)
    {
        if (go != null) _cleanup.Add(go);
        return go;
    }

    private static ItemDefinition CreateDef(string id, GameObject prefab, Vector3 logicalSize, List<GenerationRule> rules = null)
    {
        var def = ScriptableObject.CreateInstance<ItemDefinition>();
        def.itemID = id;
        def.prefab = prefab;
        def.logicalSize = logicalSize;
        def.minBounds = logicalSize;
        def.rules = rules ?? new List<GenerationRule>();
        return def;
    }

    private static GameObject CreateTemplateCube(string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = new Vector3(10000, 10000, 10000);
        return go;
    }

    private static GameObject CreateTemplateDoorSystem(string name)
    {
        var root = new GameObject(name);
        root.transform.position = new Vector3(10000, 10000, 10000);

        root.AddComponent<BoxCollider>();

        var hinge = new GameObject("HingeSlot").transform;
        hinge.SetParent(root.transform, false);
        hinge.localPosition = Vector3.zero;

        var doorSlot = new GameObject("DoorSlot").transform;
        doorSlot.SetParent(hinge, false);
        doorSlot.localPosition = Vector3.zero;

        var frame = new GameObject("Frame").transform;
        frame.SetParent(root.transform, false);
        frame.localPosition = Vector3.zero;

        var ctrl = root.AddComponent<DoorController>();
        ctrl.hingeOnLeft = true;
        ctrl.isLocked = false;
        ctrl.hinge = hinge;
        ctrl.autoAlignHinge = false;
        ctrl.hingeLocalOffset = Vector3.zero;

        var loader = root.AddComponent<DoorArtLoader>();
        loader.frameSlot = frame;
        loader.doorSlot = doorSlot;
        loader.createFallbackPrimitives = true;
        loader.rebuildOnEnable = true;

        return root;
    }

    [UnityTest]
    public IEnumerator SC1_CupSnapsOntoTable_TableSnapsOntoFloor()
    {
        var roomRoot = Track(new GameObject("RoomRoot"));
        var rb = roomRoot.AddComponent<MyGame.Adapters.Unity.RoomBuilder>();
        rb.roomSize = new Vector3(10, 2, 10);

        // Templates (not instantiated directly into the room)
        var floorPrefab = Track(CreateTemplateCube("FloorPrefab"));
        var tablePrefab = Track(CreateTemplateCube("TablePrefab"));
        var cupPrefab = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
        cupPrefab.name = "CupPrefab";
        cupPrefab.transform.position = new Vector3(10000, 10000, 10000);

        var defs = new List<ItemDefinition>
        {
            CreateDef("FloorTile", floorPrefab, new Vector3(10f, 0.2f, 10f)),
            CreateDef("Table", tablePrefab, new Vector3(1.2f, 1f, 1.2f)),
            CreateDef("Cup", cupPrefab, new Vector3(0.3f, 0.3f, 0.3f)),
        };
        rb.database = defs;

        // Blueprint (single room, minimal)
        var bp = new RoomBlueprint();
        bp.nodes.Add(new PropNode
        {
            instanceID = "Floor_0",
            itemID = "FloorTile",
            parentID = null,
            position = new SimpleVector3(0, 0, 0),
            rotation = SimpleVector3.Zero,
            containerKind = ContainerKind.Floor,
            logicalBounds = new SimpleBounds(new SimpleVector3(0, 0, 0), new SimpleVector3(10f, 0.2f, 10f)),
            facing = Facing.Up
        });
        bp.nodes.Add(new PropNode
        {
            instanceID = "Table_0",
            itemID = "Table",
            parentID = null,
            position = new SimpleVector3(0, 0.5f, 0),
            rotation = SimpleVector3.Zero,
            containerKind = ContainerKind.Table,
            logicalBounds = new SimpleBounds(new SimpleVector3(0, 0.5f, 0), new SimpleVector3(1.2f, 1f, 1.2f)),
            facing = Facing.None
        });
        bp.nodes.Add(new PropNode
        {
            instanceID = "Cup_0",
            itemID = "Cup",
            parentID = "Table_0",
            position = new SimpleVector3(0, 0, 0), // local offset
            rotation = SimpleVector3.Zero,
            containerKind = ContainerKind.Unknown,
            logicalBounds = default,
            facing = Facing.None
        });

        rb.blueprint = bp;
        rb.BuildFromGeneratedBlueprint();

        // Wait a few frames so physics transforms are synced and snapping finishes.
        yield return null;
        yield return null;

        var floor = roomRoot.transform.Find("Floor_0");
        var table = roomRoot.transform.Find("Table_0");
        var cup = table != null
            ? table.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Cup_0")
            : null;
        Assert.That(floor, Is.Not.Null);
        Assert.That(table, Is.Not.Null);
        Assert.That(cup, Is.Not.Null);

        var floorCol = floor.GetComponentInChildren<Collider>();
        var tableCol = table.GetComponentInChildren<Collider>();
        var cupCol = cup.GetComponentInChildren<Collider>();
        Assert.That(floorCol, Is.Not.Null);
        Assert.That(tableCol, Is.Not.Null);
        Assert.That(cupCol, Is.Not.Null);

        float floorSurfaceY = floorCol.bounds.max.y;
        float tableBottomY = tableCol.bounds.min.y;
        float tableTopY = tableCol.bounds.max.y;
        float cupBottomY = cupCol.bounds.min.y;

        Assert.That(Mathf.Abs(tableBottomY - floorSurfaceY), Is.LessThanOrEqualTo(SnapEpsilon), "Table should sit on floor");
        Assert.That(Mathf.Abs(cupBottomY - tableTopY), Is.LessThanOrEqualTo(SnapEpsilon), "Cup should sit on table top");

        // XY projection: cup center should be within table bounds in XZ (with epsilon).
        var c = cupCol.bounds.center;
        var tb = tableCol.bounds;
        Assert.That(c.x, Is.InRange(tb.min.x - SnapEpsilon, tb.max.x + SnapEpsilon));
        Assert.That(c.z, Is.InRange(tb.min.z - SnapEpsilon, tb.max.z + SnapEpsilon));
    }

    [UnityTest]
    public IEnumerator SC1_DoorLeafIsSolidAndReturnsToClosedWithinTolerance()
    {
        var roomRoot = Track(new GameObject("RoomRoot_Door"));
        var rb = roomRoot.AddComponent<MyGame.Adapters.Unity.RoomBuilder>();
        rb.roomSize = new Vector3(10, 2, 10);

        var floorPrefab = Track(CreateTemplateCube("FloorPrefab"));
        var doorSystemPrefab = Track(CreateTemplateDoorSystem("DoorSystemPrefab"));

        rb.database = new List<ItemDefinition>
        {
            CreateDef("FloorTile", floorPrefab, new Vector3(10f, 0.2f, 10f)),
            CreateDef("DoorSystem", doorSystemPrefab, new Vector3(1f, 3f, 0.2f)),
        };

        var bp = new RoomBlueprint();
        bp.nodes.Add(new PropNode
        {
            instanceID = "Floor_0",
            itemID = "FloorTile",
            parentID = null,
            position = new SimpleVector3(0, 0, 0),
            rotation = SimpleVector3.Zero,
            containerKind = ContainerKind.Floor,
            logicalBounds = new SimpleBounds(new SimpleVector3(0, 0, 0), new SimpleVector3(10f, 0.2f, 10f)),
            facing = Facing.Up
        });
        bp.nodes.Add(new PropNode
        {
            instanceID = "Door_0",
            itemID = "DoorSystem",
            parentID = null,
            position = new SimpleVector3(2f, 0, 0),
            rotation = SimpleVector3.Zero,
            containerKind = ContainerKind.Door,
            logicalBounds = new SimpleBounds(new SimpleVector3(2f, 0, 0), new SimpleVector3(1f, 3f, 0.2f)),
            facing = Facing.East
        });

        rb.blueprint = bp;
        rb.BuildFromGeneratedBlueprint();
        yield return null;
        yield return null;

        var doorRoot = roomRoot.transform.Find("Door_0")?.gameObject;
        Assert.That(doorRoot, Is.Not.Null);

        var ctrl = doorRoot.GetComponent<DoorController>();
        Assert.That(ctrl, Is.Not.Null);

        // Find the leaf collider and assert it's not trigger (closed should block).
        var leaf = doorRoot.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DoorLeaf");
        Assert.That(leaf, Is.Not.Null);
        var leafCol = leaf.GetComponent<Collider>();
        Assert.That(leafCol, Is.Not.Null);
        Assert.That(leafCol.isTrigger, Is.False);

        // Toggle open/close and verify it returns near closed.
        ctrl.TryOpen();
        yield return new WaitForSeconds(0.45f);
        ctrl.TryOpen(); // close
        yield return new WaitForSeconds(0.65f);

        float yaw = ctrl.hinge != null ? ctrl.hinge.localRotation.eulerAngles.y : doorRoot.transform.localRotation.eulerAngles.y;
        if (yaw > 180f) yaw -= 360f;
        Assert.That(Mathf.Abs(yaw), Is.LessThanOrEqualTo(1f), "Door should return to closeAngle within ε_closeAngle");
    }

    [UnityTest]
    public IEnumerator SC1_DoorOpensAwayFromPlayerSide_WhenStartingFromClosed()
    {
        var roomRoot = Track(new GameObject("RoomRoot_DoorDir"));
        var rb = roomRoot.AddComponent<MyGame.Adapters.Unity.RoomBuilder>();
        rb.roomSize = new Vector3(10, 2, 10);

        var floorPrefab = Track(CreateTemplateCube("FloorPrefab"));
        var doorSystemPrefab = Track(CreateTemplateDoorSystem("DoorSystemPrefab"));

        rb.database = new List<ItemDefinition>
        {
            CreateDef("FloorTile", floorPrefab, new Vector3(10f, 0.2f, 10f)),
            CreateDef("DoorSystem", doorSystemPrefab, new Vector3(1f, 3f, 0.2f)),
        };

        var bp = new RoomBlueprint();
        bp.nodes.Add(new PropNode
        {
            instanceID = "Floor_0",
            itemID = "FloorTile",
            parentID = null,
            position = new SimpleVector3(0, 0, 0),
            rotation = SimpleVector3.Zero,
            containerKind = ContainerKind.Floor,
            logicalBounds = new SimpleBounds(new SimpleVector3(0, 0, 0), new SimpleVector3(10f, 0.2f, 10f)),
            facing = Facing.Up
        });
        bp.nodes.Add(new PropNode
        {
            instanceID = "Door_0",
            itemID = "DoorSystem",
            parentID = null,
            position = new SimpleVector3(0f, 0, 0),
            rotation = SimpleVector3.Zero,
            containerKind = ContainerKind.Door,
            logicalBounds = new SimpleBounds(new SimpleVector3(0f, 0, 0), new SimpleVector3(1f, 3f, 0.2f)),
            facing = Facing.North
        });

        rb.blueprint = bp;
        rb.BuildFromGeneratedBlueprint();
        yield return null;
        yield return null;

        var doorRoot = roomRoot.transform.Find("Door_0")?.gameObject;
        Assert.That(doorRoot, Is.Not.Null);
        var ctrl = doorRoot.GetComponent<DoorController>();
        Assert.That(ctrl, Is.Not.Null);
        ctrl.isLocked = false;

        var leaf = doorRoot.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DoorLeaf");
        Assert.That(leaf, Is.Not.Null);
        var leafCol = leaf.GetComponent<Collider>();
        Assert.That(leafCol, Is.Not.Null);

        // PlayerInteraction exists only to provide a "player" Transform to DoorController.
        var player = Track(new GameObject("Player"));
        var pi = player.AddComponent<PlayerInteraction>();
        pi.debugRay = false;
        pi.debugInteract = false;
        pi.eye = player.transform; // avoid "No eye/camera assigned" warning spam

        Vector3 normal = doorRoot.transform.forward.normalized;

        Vector3 ClosedCenter() => leafCol.bounds.center;

        // Case A: player on +normal side => door should move toward -normal (dot < 0)
        player.transform.position = doorRoot.transform.position + normal * 3f;
        var closedA = ClosedCenter();
        ctrl.TryOpen();
        yield return new WaitForSeconds(0.6f);
        var openA = ClosedCenter();
        float dotA = Vector3.Dot(openA - closedA, normal);
        Assert.That(dotA, Is.LessThan(0f), "Door should open away from player side (+normal => move toward -normal)");

        // Close fully
        ctrl.TryOpen();
        yield return WaitUntilClosed(ctrl, timeoutSeconds: 3f);

        // Case B: player on -normal side => door should move toward +normal (dot > 0)
        player.transform.position = doorRoot.transform.position - normal * 3f;
        var closedB = ClosedCenter();
        ctrl.TryOpen();
        yield return new WaitForSeconds(0.6f);
        var openB = ClosedCenter();
        float dotB = Vector3.Dot(openB - closedB, normal);
        Assert.That(dotB, Is.GreaterThan(0f), "Door should open away from player side (-normal => move toward +normal)");
    }

    private static IEnumerator WaitUntilClosed(DoorController ctrl, float timeoutSeconds)
    {
        float start = Time.time;
        while (Time.time - start < timeoutSeconds)
        {
            if (ctrl != null && ctrl.hinge != null)
            {
                float yaw = ctrl.hinge.localRotation.eulerAngles.y;
                if (yaw > 180f) yaw -= 360f;
                if (Mathf.Abs(yaw) <= CloseAngleEpsilonDeg) yield break;
            }
            yield return null;
        }
        Assert.Fail("Timed out waiting for door to close");
    }
}
