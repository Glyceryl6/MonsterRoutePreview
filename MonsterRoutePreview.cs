#nullable enable
using CesiumLoader.SDK.Mods;
using UnityEngine.SceneManagement;

namespace MonsterRoutePreview {
    
    public class MonsterRoutePreview : ModBase {
        
        private MonsterRoutePreviewService? _monsterRoutes;

        public override void OnInitialize() {
            _monsterRoutes = new MonsterRoutePreviewService();
        }

        public override void OnUpdate() {
            _monsterRoutes?.Tick();
        }

        public override void OnUnload() {
            _monsterRoutes?.Dispose();
            _monsterRoutes = null;
        }
        
        public override void OnSceneUnloaded(Scene scene) => ResetSceneObjects();
        
        private void ResetSceneObjects() {
            _monsterRoutes?.Clear();
        }
        
    }
    
}