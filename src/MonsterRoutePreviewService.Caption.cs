using FairyGUI;
using UI;

namespace MonsterRoutePreview {
    internal sealed partial class MonsterRoutePreviewService {
        private void ShowCaption(UIBattleInfoPanel parent, int initial, int remaining) {
            if (_captionParent != parent || _caption == null || _caption.isDisposed) {
                if (_caption != null && !_caption.isDisposed) {
                    _caption.RemoveFromParent();
                    _caption.Dispose();
                }
                _caption = new GTextField {
                    autoSize = AutoSizeType.None,
                    touchable = false, singleLine = true
                };
                TextFormat format = _caption.textFormat;
                format.size = 16;
                _caption.textFormat = format;
                _caption.SetSize(260f, 28f);
                parent.AddChild(_caption);
                _captionParent = parent;
            }
            GTextField anchor = parent.txt_DicePoint;
            if (anchor != null) _caption.SetXY(anchor.x, anchor.y + anchor.height + 3f);
            _caption.text = "怪物移动：" + remaining + " / " + initial;
        }
    }
}
