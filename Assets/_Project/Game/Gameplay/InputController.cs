using System;
using PuzzleStudio.Game.Bootstrap;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace PuzzleStudio.Game.Gameplay
{
    /// <summary>
    /// Turns raw pointer/keyboard input into board actions: hover, tap, drag &amp; drop, right-click,
    /// and gameplay shortcuts. Pointer input over UI Toolkit buttons/panels is ignored.
    /// (Gamepad grid cursor: milestone 9.)
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
        /// <summary>Arrow keys (sliding puzzle): dx, dy with y pointing down.</summary>
        public event Action<int, int> OnDirection;
        public bool InputEnabled = true;

        public event Action<int> OnTap;
        public event Action<int> OnDragStart;
        public event Action<int, int> OnDrop;        // from, to (-1 = outside)
        public event Action<int> OnSecondaryTap;     // right click
        public event Action OnUndo;
        public event Action OnHint;
        public event Action OnRestart;
        public event Action<bool> OnPreview;          // held / released

        const float DragThresholdPx = 10f;

        int _pressCell = -1;
        Vector2 _pressPos;
        bool _dragging;
        bool _previewHeld;

        void Update()
        {
            HandleKeyboard();
            HandlePointer();
        }

        void HandleKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!Viewport.KeyboardEnabled)
            {
                if (_previewHeld) { _previewHeld = false; OnPreview?.Invoke(false); }
                return;
            }

            bool previewKey = InputEnabled && kb.spaceKey.isPressed;
            if (previewKey != _previewHeld) { _previewHeld = previewKey; OnPreview?.Invoke(previewKey); }
            if (!InputEnabled) return;

            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            if (ctrl && kb.zKey.wasPressedThisFrame) OnUndo?.Invoke();
            else if (kb.backspaceKey.wasPressedThisFrame) OnUndo?.Invoke();
            if (kb.hKey.wasPressedThisFrame) OnHint?.Invoke();
            if (kb.rKey.wasPressedThisFrame && !ctrl) OnRestart?.Invoke();
            if (kb.leftArrowKey.wasPressedThisFrame) OnDirection?.Invoke(-1, 0);
            else if (kb.rightArrowKey.wasPressedThisFrame) OnDirection?.Invoke(1, 0);
            else if (kb.upArrowKey.wasPressedThisFrame) OnDirection?.Invoke(0, -1);
            else if (kb.downArrowKey.wasPressedThisFrame) OnDirection?.Invoke(0, 1);
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
                Board.SetHover(-1);
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

            if (!pointer.press.isPressed)
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
