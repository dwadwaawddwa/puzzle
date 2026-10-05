using System;
using PuzzleStudio.Core.Util;
using PuzzleStudio.Game.Bootstrap;
using PuzzleStudio.Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.Gameplay
{
    /// <summary>
    /// Turns raw input into board actions:
    /// mouse/touch (hover, tap, drag &amp; drop, right-click), keyboard and gamepad (board cursor, shortcuts).
    /// Pointer input over UI Toolkit buttons/panels is ignored.
    /// <code>
    ///                 keyboard              gamepad
    /// move cursor     arrows / WASD         D-pad / left stick
    /// pick / place    Enter                 A
    /// turn back       Q                     RB      (rotate mode)
    /// hint            H                     X
    /// preview (hold)  Space                 Y
    /// undo            Ctrl+Z / Backspace    LB
    /// restart         R                     View
    /// pause / cancel  Esc                   Menu / B   (GameFlow)
    /// </code>
    /// In the sliding mode the directions push tiles into the gap instead of moving a cursor.
    /// </summary>
    public sealed class InputController : MonoBehaviour
    {
        public BoardView Board;
        public Camera Camera;
        public VisualElement UiRoot;
        public GameViewport Viewport = new GameViewport();
        public Func<int, bool> CanPick;
        /// <summary>False for modes where a drag gesture only means "tap this piece" (sliding) or nothing (rotate).</summary>
        public Func<bool> AllowFreeDrag = () => true;
        /// <summary>True when directions push tiles (sliding) rather than moving the cursor.</summary>
        public Func<bool> DirectionSlides = () => false;
        /// <summary>Directions in sliding mode: dx, dy with y pointing down.</summary>
        public event Action<int, int> OnDirection;

        /// <summary>Board input on/off. The frame it turns on is skipped: the A / Enter that pressed "Play",
        /// "Next" or "Resume" must not also act on the board.</summary>
        public bool InputEnabled
        {
            get => _inputEnabled;
            set
            {
                if (value && !_inputEnabled) _enabledFrame = Time.frameCount;
                _inputEnabled = value;
            }
        }
        bool _inputEnabled = true;
        int _enabledFrame = -1;

        public event Action<int> OnTap;
        public event Action<int> OnDragStart;
        public event Action<int, int> OnDrop;        // from, to (-1 = outside)
        public event Action<int> OnSecondaryTap;     // right click / RB / Q
        public event Action OnUndo;
        public event Action OnHint;
        public event Action OnRestart;
        public event Action<bool> OnPreview;          // held / released

        const float DragThresholdPx = 10f;
        const float StickDeadZone = 0.55f;

        int _pressCell = -1;
        Vector2 _pressPos;
        bool _dragging;
        bool _previewHeld;
        RepeatTimer _repeat = new RepeatTimer(0.32f, 0.11f);

        void Update()
        {
            var kb = Viewport.KeyboardEnabled ? Keyboard.current : null;
            var pad = Gamepad.current;
            if (Time.frameCount == _enabledFrame) { kb = null; pad = null; }
            HandleShortcuts(kb, pad);
            HandleNavigation(kb, pad);
            HandlePointer();
        }

        void HandleShortcuts(Keyboard kb, Gamepad pad)
        {
            bool previewHeld = InputEnabled && ((kb != null && kb.spaceKey.isPressed) || (pad != null && pad.buttonNorth.isPressed));
            if (previewHeld != _previewHeld) { _previewHeld = previewHeld; OnPreview?.Invoke(previewHeld); }
            if (!InputEnabled) return;

            bool ctrl = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
            bool undo = (kb != null && ((ctrl && kb.zKey.wasPressedThisFrame) || kb.backspaceKey.wasPressedThisFrame))
                        || (pad != null && pad.leftShoulder.wasPressedThisFrame);
            bool hint = (kb != null && kb.hKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame);
            bool restart = (kb != null && kb.rKey.wasPressedThisFrame && !ctrl) || (pad != null && pad.selectButton.wasPressedThisFrame);
            if (undo) OnUndo?.Invoke();
            if (hint) OnHint?.Invoke();
            if (restart) OnRestart?.Invoke();
        }

        void HandleNavigation(Keyboard kb, Gamepad pad)
        {
            if (!InputEnabled || Board == null) { _repeat.Reset(); return; }

            var dir = ReadDirection(kb, pad);
            int key = dir == Vector2Int.zero ? 0 : (dir.x + 2) * 10 + dir.y + 2;
            if (_repeat.Tick(key, Time.unscaledDeltaTime))
            {
                if (DirectionSlides()) OnDirection?.Invoke(dir.x, dir.y);
                else if (!Board.CursorVisible) Board.ShowCursor(Board.HoverCell);   // first press: show where we are
                else Board.MoveCursor(dir.x, dir.y);
            }
            if (DirectionSlides()) return;

            bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                           || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (confirm)
            {
                if (!Board.CursorVisible) Board.ShowCursor(Board.HoverCell);
                else OnTap?.Invoke(Board.CursorCell);
            }
            bool secondary = (kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame);
            if (secondary && Board.CursorVisible) OnSecondaryTap?.Invoke(Board.CursorCell);
        }

        /// <summary>D-pad, then left stick, then arrows / WASD. y points down.</summary>
        static Vector2Int ReadDirection(Keyboard kb, Gamepad pad)
        {
            if (pad != null)
            {
                var d = pad.dpad;
                if (d.left.isPressed) return new Vector2Int(-1, 0);
                if (d.right.isPressed) return new Vector2Int(1, 0);
                if (d.up.isPressed) return new Vector2Int(0, -1);
                if (d.down.isPressed) return new Vector2Int(0, 1);
                var s = pad.leftStick.ReadValue();
                if (s.magnitude > StickDeadZone)
                    return Mathf.Abs(s.x) > Mathf.Abs(s.y) ? new Vector2Int(s.x > 0 ? 1 : -1, 0) : new Vector2Int(0, s.y > 0 ? -1 : 1);
            }
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) return new Vector2Int(-1, 0);
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) return new Vector2Int(1, 0);
                if (kb.upArrowKey.isPressed || kb.wKey.isPressed) return new Vector2Int(0, -1);
                if (kb.downArrowKey.isPressed || kb.sKey.isPressed) return new Vector2Int(0, 1);
            }
            return Vector2Int.zero;
        }

        void HandlePointer()
        {
            var pointer = Pointer.current;
            if (pointer == null || Board == null || Camera == null) return;

            bool hasPointer = Viewport.TryGetPointer(out Vector2 screen);
            Vector3 world = hasPointer
                ? Camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -Camera.transform.position.z))
                : new Vector3(1e6f, 1e6f, 0f);   // outside the board
            world.z = 0f;

            if (!InputEnabled)
            {
                CancelDrag();
                if (!InputModeTracker.UsesNavigation) Board.SetHover(-1);
                return;
            }

            bool overUi = !hasPointer || Viewport.IsOverUI(screen, UiRoot);

            if (pointer.press.wasPressedThisFrame)
            {
                _pressCell = overUi ? -1 : Board.CellAt(world);
                _pressPos = screen;
                _dragging = false;
            }

            if (pointer.press.isPressed && _pressCell >= 0)
            {
                if (!_dragging && (screen - _pressPos).magnitude > DragThresholdPx && (CanPick?.Invoke(_pressCell) ?? true) && AllowFreeDrag())
                {
                    _dragging = true;
                    Board.BeginDrag(_pressCell, world);
                    OnDragStart?.Invoke(_pressCell);
                }
                if (_dragging) Board.DragTo(world);
            }

            if (pointer.press.wasReleasedThisFrame && _pressCell >= 0)
            {
                int from = _pressCell;
                _pressCell = -1;
                if (_dragging)
                {
                    _dragging = false;
                    int to = Board.CellAt(world);
                    Board.EndDrag();
                    OnDrop?.Invoke(from, to);
                }
                else if (Board.CellAt(world) == from || !AllowFreeDrag())
                {
                    // Sliding: swiping a tile toward the gap is the same as tapping it.
                    OnTap?.Invoke(from);
                }
            }

            // With a gamepad / keyboard the cursor owns the hover highlight.
            if (!pointer.press.isPressed && !InputModeTracker.UsesNavigation)
                Board.SetHover(overUi ? -1 : Board.CellAt(world));

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame && !overUi && !_dragging)
            {
                int cell = Board.CellAt(world);
                if (cell >= 0) OnSecondaryTap?.Invoke(cell);
            }
        }

        public void CancelDrag()
        {
            if (_dragging)
            {
                Board.EndDrag();
                OnDrop?.Invoke(-1, -1);
            }
            _dragging = false;
            _pressCell = -1;
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus)
            {
                CancelDrag();
                if (_previewHeld) { _previewHeld = false; OnPreview?.Invoke(false); }
            }
        }
    }
}
