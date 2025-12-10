using System.Collections.Generic;
using Core = MyGame.Core;
using Imported = MyGame_1.Core;

namespace MyGame.Adapters.Imported
{
    public static class ImportedCoreMapper
    {
        public static Core.SimpleVector3 ToCore(this Imported.SimpleVector3 v)
        {
            return new Core.SimpleVector3(v.x, v.y, v.z);
        }

        public static Core.PropNode ToCore(this Imported.PropNode n)
        {
            var pn = new Core.PropNode();
            pn.instanceID = n.instanceID;
            pn.itemID = n.itemID;
            pn.parentID = n.parentID;
            pn.position = n.position.ToCore();
            pn.rotation = n.rotation.ToCore();
            return pn;
        }

        public static Core.RoomBlueprint ToCore(this Imported.RoomBlueprint bp)
        {
            var cb = new Core.RoomBlueprint();
            foreach (var n in bp.nodes)
            {
                cb.nodes.Add(n.ToCore());
            }
            return cb;
        }
    }
}
