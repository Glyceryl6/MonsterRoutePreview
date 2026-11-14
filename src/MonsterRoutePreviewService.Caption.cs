using System;
using System.Globalization;
using FairyGUI;
using UI;

namespace MonsterRoutePreview {
    internal sealed partial class MonsterRoutePreviewService {
        private GTextField? _captionAnchor;

        private void ShowCaption(UIBattleInfoPanel parent, int initial) {
            GTextField? anchor = parent.txt_DicePoint;
            if (anchor == null || anchor.isDisposed || !parent.onStage ||
                (parent.showDicePoint != null && parent.showDicePoint.playing)) {
                HideCaption();
                return;
            }

            // Keep the label inside the native battle HUD. Its visibility and
            // position now follow that HUD when a fight overlay opens or the
            // scene changes; a separate GRoot overlay leaked above fights.
            if (_captionParent != parent || _captionAnchor != anchor ||
                _caption == null || _caption.isDisposed || _caption.parent != parent) {
                HideCaption();
                _captionParent = parent;
                _captionAnchor = anchor;
                _caption = new GTextField {
                    name = "AstralProbe.MonsterMoveCount",
                    autoSize = AutoSizeType.None,
                    touchable = false,
                    singleLine = true,
                    align = AlignType.Center,
                    verticalAlign = anchor.verticalAlign
                };
                // The original counter supplies the font face, size and color.
                // Do not enlarge it or add a second fuzzy black stroke.
                TextFormat native = _caption.textFormat;
                native.CopyFrom(anchor.textFormat);
                _caption.textFormat = native;
                _caption.stroke = 0;
                _caption.sortingOrder = anchor.sortingOrder + 1;
                parent.AddChild(_caption);
            }
            _caption.SetSize(Math.Max(1f, anchor.width), Math.Max(1f, anchor.height));
            _caption.SetXY(anchor.x, anchor.y);
            _caption.text = initial.ToString(CultureInfo.InvariantCulture);
            _caption.visible = true;
        }

        private void HideCaption() {
            if (_caption != null && !_caption.isDisposed) {
                _caption.RemoveFromParent();
                _caption.Dispose();
            }
            _caption = null;
            _captionParent = null;
            _captionAnchor = null;
        }
    }
}
