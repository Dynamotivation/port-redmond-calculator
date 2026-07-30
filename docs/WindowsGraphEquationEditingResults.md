# Windows graph equation editing conformance results

Tested on 2026-07-30 against Microsoft Calculator 11.2606.0.0 (Store) on
Windows 10.0.26200.8875. The active regional format was `de-DE`, with `,` as
the decimal separator and `;` as the list separator.

This document records the confirmed behavior used by the Avalonia graph editor.
The detailed manual procedures remain in
`WindowsGraphEquationEditingTestSuite.md`.

## Confirmed behavior

| Area | Windows behavior |
|---|---|
| Live editor | One structured math editor is used for valid and empty expressions. Variables are math italic while typing. |
| Submission | Main Enter and focus loss submit. Numpad Enter does not. Main Enter advances to the next row only after a changed expression; keypad Submit commits and retains the row. |
| Graph update | Draft edits do not move the graph. Submission updates presentation and graph together. |
| Fractions | Numerator and denominator edits retain their structural boundaries. Down moves from numerator to denominator. |
| Powers and roots | Exponent, fraction-base, and radicand edits remain within their structural group. |
| Functions | Keypad templates create semantic functions. Literal `sin(x)` or `sqrt(` remains literal and is invalid on submission. |
| Complex deletion | First Backspace previews the structural deletion; the second performs it. Fractions are one unit. Powers delete with their base. Function argument deletion leaves the function name. |
| Invalid input | Invalid or incomplete input is retained as linear text with an error presentation and can be repaired. |
| Clipboard | Plain multiline paste keeps the first line. Windows also publishes rich structured formats for formatted math. |
| Locale | `1,5*x` is valid under `de-DE`; the configured list separator is accepted. |
| Limit | The exact input cap is 2,048 characters. |
| Context menu | Opening or dismissing the menu alone does not submit the draft. |

## Important safety divergence

Windows Calculator exited while submitting:

```text
π+θ×2÷3−1≤x≥0
```

The Avalonia implementation intentionally does not reproduce this crash. The
expression must either parse safely or produce a normal graphing parse error.

## Applied Avalonia coverage

- Live structured editing for empty, drafted, and committed valid equations.
- Structural serialization that preserves fraction, power, radical, delimiter,
  and function argument grouping.
- Semantic graph-keypad templates.
- Main Enter, numpad Enter, focus-loss, keypad Submit, and changed-row focus
  semantics.
- Two-stage complex Backspace and local editor undo.
- Fraction Up/Down navigation.
- First-line paste behavior and a context menu that does not commit merely by
  opening.
- The 2,048-character cap.
- Locale-aware decimal and list-separator normalization.
- Safe handling of the Windows crash expression.

## Known remaining differences

- Windows publishes and consumes RTF/UnicodeMath clipboard formats. Avalonia
  currently guarantees plain-text copy/paste and restores structure by parsing
  that text; it does not publish the Windows-specific RTF format set.
- Full structured range selection and `Select all` are not yet provided by the
  CSharpMath editor integration.
- Windows shows keypad `sqrt()` and `abs()` as textual templates until submit;
  Avalonia displays their radical/bar structure immediately.
- Touch, pen, handwriting, IME composition, localized digit input, high
  contrast, and display-scale permutations were not exercised in the reference
  session.
