using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Blender-style numeric entry for transform modals. Typing a digit / '-' / '.' / '/' (or an operator)
    /// activates typed-value mode; Tab advances to the next axis component; Backspace deletes. Each
    /// component is a small math expression evaluated by <see cref="ExpressionEvaluator"/>.
    /// </summary>
    public sealed class NumericInput
    {
        readonly List<string> _components = new List<string> { string.Empty };
        int _active;
        bool _hasInput;

        /// <summary>True once the user has typed anything (typed-value mode is engaged).</summary>
        public bool HasInput => _hasInput;

        /// <summary>Index of the component currently being edited (advanced by Tab).</summary>
        public int ActiveComponent => _active;

        /// <summary>Number of components entered so far (at least 1).</summary>
        public int ComponentCount => _components.Count;

        /// <summary>Clear all state back to empty.</summary>
        public void Reset()
        {
            _components.Clear();
            _components.Add(string.Empty);
            _active = 0;
            _hasInput = false;
        }

        /// <summary>
        /// Feed a KeyDown event. Returns true if the key was consumed as numeric input. Returns false for
        /// keys the numeric layer doesn't own (letting the modal handle Enter/Esc/axis keys/etc.).
        /// </summary>
        public bool HandleKey(Event e)
        {
            if (e.type != EventType.KeyDown) return false;

            if (e.keyCode == KeyCode.Tab)
            {
                _active++;
                while (_components.Count <= _active)
                    _components.Add(string.Empty);
                _hasInput = true;
                return true;
            }

            if (e.keyCode == KeyCode.Backspace)
            {
                if (!_hasInput) return false;
                string cur = _components[_active];
                if (cur.Length > 0)
                    _components[_active] = cur.Substring(0, cur.Length - 1);
                else if (_active > 0)
                    _active--;
                RecomputeHasInput();
                return true;
            }

            char c = e.character;
            // Some keypad keys don't emit characters reliably; map them explicitly.
            if (c == '\0')
                c = KeypadChar(e.keyCode);

            if (IsAccepted(c))
            {
                _components[_active] += c;
                _hasInput = true;
                return true;
            }

            return false;
        }

        static char KeypadChar(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.Keypad0: return '0';
                case KeyCode.Keypad1: return '1';
                case KeyCode.Keypad2: return '2';
                case KeyCode.Keypad3: return '3';
                case KeyCode.Keypad4: return '4';
                case KeyCode.Keypad5: return '5';
                case KeyCode.Keypad6: return '6';
                case KeyCode.Keypad7: return '7';
                case KeyCode.Keypad8: return '8';
                case KeyCode.Keypad9: return '9';
                case KeyCode.KeypadPeriod: return '.';
                case KeyCode.KeypadDivide: return '/';
                case KeyCode.KeypadMultiply: return '*';
                case KeyCode.KeypadMinus: return '-';
                case KeyCode.KeypadPlus: return '+';
                default: return '\0';
            }
        }

        static bool IsAccepted(char c)
        {
            return (c >= '0' && c <= '9') ||
                   c == '.' || c == '-' || c == '+' || c == '*' || c == '/' ||
                   c == '(' || c == ')' || c == ' ';
        }

        void RecomputeHasInput()
        {
            for (int i = 0; i < _components.Count; i++)
            {
                if (_components[i].Length > 0)
                {
                    _hasInput = true;
                    return;
                }
            }
            _hasInput = _active > 0;
        }

        /// <summary>Evaluate a component; returns false if empty or unparseable.</summary>
        public bool TryGetComponent(int index, out float value)
        {
            value = 0f;
            if (index < 0 || index >= _components.Count) return false;
            return ExpressionEvaluator.TryEvaluate(_components[index], out value);
        }

        /// <summary>Evaluate a component or return <paramref name="fallback"/> if it's empty/invalid.</summary>
        public float GetComponentOr(int index, float fallback)
        {
            return TryGetComponent(index, out float v) ? v : fallback;
        }

        /// <summary>Raw text of a component (for display).</summary>
        public string RawComponent(int index)
        {
            return (index >= 0 && index < _components.Count) ? _components[index] : string.Empty;
        }

        /// <summary>HUD-friendly rendering, e.g. "2.5, [0], " with the active component marked.</summary>
        public string Display()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _components.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                string raw = _components[i];
                if (i == _active)
                    sb.Append('[').Append(raw).Append(']');
                else
                    sb.Append(raw.Length == 0 ? "0" : raw);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Tiny recursive-descent evaluator for the numeric entry: <c>+ - * /</c>, unary sign, parentheses,
    /// and the constants pi/tau/e. Deliberately avoids DataTable.Compute (not available/portable).
    /// </summary>
    public static class ExpressionEvaluator
    {
        public static bool TryEvaluate(string expr, out float result)
        {
            result = 0f;
            if (string.IsNullOrWhiteSpace(expr)) return false;
            try
            {
                var p = new Parser(expr);
                float v = p.ParseExpression();
                if (!p.AtEnd) return false;
                if (float.IsNaN(v) || float.IsInfinity(v)) return false;
                result = v;
                return true;
            }
            catch
            {
                return false;
            }
        }

        sealed class Parser
        {
            readonly string _s;
            int _i;

            public Parser(string s) { _s = s; _i = 0; }

            public bool AtEnd
            {
                get { SkipWhitespace(); return _i >= _s.Length; }
            }

            void SkipWhitespace()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            char Peek()
            {
                SkipWhitespace();
                return _i < _s.Length ? _s[_i] : '\0';
            }

            public float ParseExpression()
            {
                float v = ParseTerm();
                while (true)
                {
                    char c = Peek();
                    if (c == '+') { _i++; v += ParseTerm(); }
                    else if (c == '-') { _i++; v -= ParseTerm(); }
                    else break;
                }
                return v;
            }

            float ParseTerm()
            {
                float v = ParseFactor();
                while (true)
                {
                    char c = Peek();
                    if (c == '*') { _i++; v *= ParseFactor(); }
                    else if (c == '/') { _i++; float d = ParseFactor(); v = d == 0f ? 0f : v / d; }
                    else break;
                }
                return v;
            }

            float ParseFactor()
            {
                char c = Peek();
                if (c == '-') { _i++; return -ParseFactor(); }
                if (c == '+') { _i++; return ParseFactor(); }
                return ParsePrimary();
            }

            float ParsePrimary()
            {
                char c = Peek();
                if (c == '(')
                {
                    _i++;
                    float v = ParseExpression();
                    if (Peek() != ')') throw new FormatException("expected ')'");
                    _i++;
                    return v;
                }

                if (char.IsLetter(c))
                {
                    int start = _i;
                    while (_i < _s.Length && char.IsLetter(_s[_i])) _i++;
                    string id = _s.Substring(start, _i - start).ToLowerInvariant();
                    switch (id)
                    {
                        case "pi": return Mathf.PI;
                        case "tau": return Mathf.PI * 2f;
                        case "e": return (float)Math.E;
                        default: throw new FormatException("unknown identifier '" + id + "'");
                    }
                }

                SkipWhitespace();
                int numStart = _i;
                while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.')) _i++;
                if (_i == numStart) throw new FormatException("expected number");
                return float.Parse(_s.Substring(numStart, _i - numStart), CultureInfo.InvariantCulture);
            }
        }
    }
}
