#nullable enable
using System;
using System.Globalization;
using FairyGUI;
using UI;
using UnityEngine;

namespace MonsterRoutePreview {
    internal sealed partial class MonsterRoutePreviewService {
        private GComponent? _captionLayer;
        private GTextField? _captionAnchor;
        private float _restingCenterY;
        private float _restingHeight;
        private UIBattleInfoPanel? _restingParent;

        // Save the resting native text rectangle before any monster's dice animation.
        // The transition moves txt_DicePoint, so sampling only at animation time
        // incorrectly places a custom counter near the top-left corner.
        private void WarmCaptionAnchor(BattleInfoPanel? native) {
            UIBattleInfoPanel? parent = native?.UIObject;
            if (parent == null || parent.isDisposed || !parent.onStage) return;
            GTextField? original = parent.txt_DicePoint;
            if (original == null || original.isDisposed) return;
            if (_restingParent == parent) return;
            _restingParent = parent;
            _restingHeight = Math.Max(1f, original.height);
            _restingCenterY = original.y + _restingHeight * .5f;
        }

        private void ShowCaption(UIBattleInfoPanel parent, int initial) {
            GTextField? anchor = parent.txt_DicePoint;
            if (anchor == null || anchor.isDisposed || !parent.onStage || GRoot.inst == null) {
                HideCaption();
                return;
            }
            if (_restingParent != parent) WarmCaptionAnchor(_battle?.battleInfo);
            if (_captionParent != parent || _captionAnchor != anchor ||
                _caption == null || _caption.isDisposed || _captionLayer == null ||
                _captionLayer.isDisposed || _captionLayer.parent != GRoot.inst) {
                HideCaption();
                _captionParent = parent;
                _captionAnchor = anchor;
                _captionLayer = new GComponent {
                    name = "MonsterRoutePreview.MoveCountLayer",
                    touchable = false
                };
                GRoot.inst.AddChild(_captionLayer);
                _caption = new GTextField {
                    name = "MonsterRoutePreview.MoveCount",
                    autoSize = AutoSizeType.None,
                    touchable = false,
                    singleLine = true,
                    align = AlignType.Center,
                    verticalAlign = anchor.verticalAlign
                };
                TextFormat native = _caption.textFormat;
                native.CopyFrom(anchor.textFormat);
                _caption.textFormat = native;
                _caption.stroke = anchor.stroke;
                _caption.strokeColor = anchor.strokeColor;
                _caption.shadowOffset = anchor.shadowOffset;
                _captionLayer.AddChild(_caption);
            }

            // The native monster counter is behind TipsWindow; a BattleInfoPanel
            // child is not visible during the FIRST roll. Draw above it, but
            // retain its exact original font and effect fields.
            int order = parent.sortingOrder;
            if (UIManager.hasInstance) {
                // Access the already-open tips window, never create another one.
                foreach (BaseWindow window in UIManager.inst.propUpWindows) {
                    if (window is TipsWindow && window.isShowing)
                        order = Math.Max(order, window.sortingOrder);
                }
            }
            GRoot root = GRoot.inst;
            _captionLayer!.sortingOrder = Math.Min(32000, Math.Max(1000, order + 1));
            _captionLayer.SetSize(root.width, root.height);
            float nativeCenter = _restingParent == parent && _restingHeight > 0f
                ? _restingCenterY : anchor.y + anchor.height * .5f;
            Vector2 global = parent.LocalToGlobal(new Vector2(0f, nativeCenter));
            Vector2 local = root.GlobalToLocal(global);
            float height = _restingParent == parent && _restingHeight > 0f
                ? _restingHeight : Math.Max(1f, anchor.height);
            _caption!.SetSize(root.width, height);
            _caption.SetXY(0f, local.y - height * .5f);
            _caption.stroke = anchor.stroke;
            _caption.strokeColor = anchor.strokeColor;
            _caption.text = initial.ToString(CultureInfo.InvariantCulture);
            _caption.visible = true;
        }

        private void HideCaption() {
            if (_captionLayer is { isDisposed: false }) {
                _captionLayer.RemoveFromParent();
                _captionLayer.Dispose();
            } else if (_caption is { isDisposed: false }) {
                _caption.RemoveFromParent();
                _caption.Dispose();
            }
            _captionLayer = null;
            _caption = null;
            _captionParent = null;
            _captionAnchor = null;
        }

        private void ResetCaptionAnchor() {
            HideCaption();
            _restingParent = null;
            _restingCenterY = _restingHeight = 0f;
        }
    }
}
