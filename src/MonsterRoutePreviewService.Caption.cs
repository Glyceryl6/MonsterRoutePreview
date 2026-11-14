#nullable enable
using System;
using FairyGUI;
using UI;
using UnityEngine;

namespace MonsterRoutePreview {
    internal sealed partial class MonsterRoutePreviewService {
        private GComponent? _captionLayer;
        private GRoot? _captionRoot;

        private void ShowCaption(UIBattleInfoPanel parent, int initial, int remaining) {
            GRoot root = GRoot.inst;
            GTextField? anchor = parent.txt_DicePoint;
            if (anchor == null || !parent.onStage) { HideCaption(); return; }
            if (_captionParent != parent || _caption == null || _caption.isDisposed ||
                _captionLayer == null || _captionLayer.isDisposed || _captionRoot != root) {
                HideCaption();
                _captionRoot = root;
                _captionLayer = new GComponent { touchable = false, opaque = false, sortingOrder = 29000 };
                _captionLayer.SetSize(root.width, root.height);
                root.AddChild(_captionLayer);
                _caption = new GTextField {
                    autoSize = AutoSizeType.None, touchable = false, singleLine = true,
                    align = AlignType.Center, verticalAlign = VertAlignType.Middle
                };
                TextFormat format = _caption.textFormat;
                format.CopyFrom(anchor.textFormat);
                format.size = Math.Min(25, Math.Max(14, format.size));
                _caption.textFormat = format;
                // TextFormat.CopyFrom does not copy effects such as text outlines.
                _caption.stroke = anchor.stroke;
                _caption.strokeColor = anchor.strokeColor;
                _caption.SetSize(270f, Math.Max(28f, anchor.height));
                _captionLayer.AddChild(_caption);
                _captionParent = parent;
            }
            _captionLayer.SetSize(root.width, root.height);
            _caption.text = initial.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Vector2 center = root.GlobalToLocal(anchor.LocalToGlobal(new Vector2(
                anchor.width * .5f, anchor.height * .5f)));
            // The native dice label supplies the location and typographic style;
            // the overlay is above its transient animation and the tips window.
            _caption.SetXY(center.x - _caption.width * .5f, center.y - _caption.height * .5f);
        }

        private void HideCaption() {
            if (_captionLayer != null && !_captionLayer.isDisposed) {
                _captionLayer.RemoveFromParent();
                _captionLayer.Dispose();
            }
            _captionLayer = null;
            _caption = null;
            _captionParent = null;
            _captionRoot = null;
        }
    }
}
