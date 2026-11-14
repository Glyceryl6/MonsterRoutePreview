using CesiumLoader.SDK.Manifests;
using CesiumLoader.SDK.Mods;

namespace MonsterRoutePreview {
    
    public static class ModEntry {
        
        public static void Main() {
            SdkManifest.ExportSidecar();
            ModBase.Run(new MonsterRoutePreview());
        }
        
    }
}