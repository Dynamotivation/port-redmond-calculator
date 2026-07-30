# Windows graph equation editing conformance suite

This is a manual test suite for establishing the behavior of the original
Microsoft Calculator graph equation editor on Windows. It focuses on the
single `MathRichEditBox` used by the Windows application and, in particular,
on how edits preserve or change the structure of already-formatted math.

The results are intended to be compared with the Avalonia graphing rewrite.
Do not infer a result from how the expression would normally be parsed. Record
what Windows actually displays and graphs.

## Test environment

Record this before testing:

| Field | Value |
|---|---|
| Windows version and build | |
| Calculator version | |
| Calculator source/build type | Store / Dev / self-built |
| Display scale | |
| Calculator window size | |
| Input language and keyboard layout | |
| Windows regional format | |
| Decimal separator | |
| List separator | |
| Physical keyboard / touch / mouse / pen | |

Use a new Graphing Calculator session unless a case says otherwise. Keep the
equation panel visible. When a setup expression is given, type it, press
Enter, and verify that it is formatted before performing the test action.

Use these result values:

- **PASS**: Windows satisfies the stated invariant.
- **FAIL**: Windows violates the stated invariant.
- **OBSERVED**: the case asks for a behavioral observation rather than a
  predetermined result.
- **BLOCKED**: the installed Windows build does not expose the feature.

For every failure or surprising observation, capture screenshots immediately
before editing, during editing with the caret visible, and after submission.

## P0 smoke suite

Run these first. They cover the behaviors most likely to expose a mismatch.

| ID | Setup | Action | Expected invariant |
|---|---|---|---|
| SM-01 | Commit `x` | Click the `x`, type `+1`, press Enter | The result represents `x+1`; Enter submits and formats it. |
| SM-02 | Commit `x/1` | Click at the end of the numerator `x`, type `+11`, press Enter | The entire numerator becomes `x+11`. The result must be `(x+11)/1`, not `x+(11/1)`. |
| SM-03 | Commit `x/1` | Click at the start of the denominator `1`, type `2+`, press Enter | The inserted text remains in the denominator. Record whether Windows produces `1/(2+1)` or another caret-specific ordering. |
| SM-04 | Commit `(x+1)/(x-1)` | Insert `2*` before the numerator `x`, press Enter | The edit remains inside the numerator and the fraction boundary is preserved. |
| SM-05 | Commit `x^2` | Click in the exponent after `2`, type `+1`, press Enter | The exponent becomes `2+1`; the result must not become `x^2+1`. |
| SM-06 | Commit `(x/2)^3` | Add `+1` to the numerator, press Enter | The base remains the complete fraction and the exponent remains attached to that base. |
| SM-07 | Commit `sin(x)` | Insert `+1` after `x`, press Enter | The addition remains inside the function argument. |
| SM-08 | Commit `x/1` | Modify the numerator, then click the graph area | Focus loss submits and formats the same expression that Enter would. |
| SM-09 | Commit `x/1` | Place the caret beside the fraction and press Backspace once, then again | Record the first and second keypress behavior. A complex math group may be selected for preview before deletion. |
| SM-10 | Start an empty equation | Type `x`, compare its glyph while focused, press Enter, compare again | Record font, italic angle, baseline, size, and spacing before and after formatting. |
| SM-11 | Commit `x/1` | Edit the numerator, press Enter, immediately continue typing | Enter must submit the edit. Record where the caret remains and where subsequent text is inserted. |
| SM-12 | Commit `x/1` | Edit the numerator using the on-screen `+`, `1`, `1` buttons, then submit | Keypad input must target the same numerator and preserve its structure. |

## A. Submission and focus

| ID | Setup | Action | Expected or observation |
|---|---|---|---|
| SUB-01 | Empty row | Type `x`, press the main Enter key | **PASS** if the equation submits, formats, and graphs. |
| SUB-02 | Empty row | Type `x`, press numpad Enter | Record whether it behaves identically to the main Enter key. |
| SUB-03 | Empty row | Type `x`, click the graph area | **PASS** if focus loss submits, formats, and graphs. |
| SUB-04 | Commit `x+1` | Focus it without changing anything, press Enter | Record whether Windows performs a visible reformat or leaves it unchanged. |
| SUB-05 | Commit `x+1` | Focus it without changing anything, click elsewhere | Record whether it differs from SUB-04. |
| SUB-06 | Commit `x` | Type `+1`, press Enter | **PASS** if the edited expression is reparsed and graph output changes immediately. |
| SUB-07 | Commit `x` | Type `+1`, click elsewhere | **PASS** if the result is semantically identical to SUB-06. |
| SUB-08 | Commit `x` | Type `+1`, click the Submit keypad button | Record focus, caret, new-row creation, and formatting behavior. |
| SUB-09 | Commit `x` | Open its context menu, then close the menu without choosing an item | **PASS** if opening the context menu alone does not unexpectedly submit or alter the equation. |
| SUB-10 | Commit `x` | Edit it, open the context menu, then dismiss the menu | Record whether submission occurs when the menu opens, closes, or only when focus moves elsewhere. |
| SUB-11 | Commit `x` | Edit it, switch to graph-only view and return | Record whether the edit is submitted, retained as a draft, or discarded. |
| SUB-12 | Commit `x` | Edit it, navigate to another calculator mode and return | Record persistence, formatting, caret, graph, and error state. |

## B. Fraction boundaries

For every fraction case, take a screenshot while the caret is visibly inside
the numerator or denominator.

| ID | Setup | Action | Expected invariant |
|---|---|---|---|
| FRA-01 | Commit `x/1` | Append `+11` to numerator `x`, submit | Result is `(x+11)/1`, not `x+(11/1)`. |
| FRA-02 | Commit `x/1` | Insert `11+` before numerator `x`, submit | Result is `(11+x)/1`. |
| FRA-03 | Commit `x/1` | Insert `+11` immediately after the fraction, submit | Result is `(x/1)+11`; this establishes the outside-boundary caret position. |
| FRA-04 | Commit `x/1` | Append `+2` to denominator `1`, submit | Result is `x/(1+2)`, not `(x/1)+2`. |
| FRA-05 | Commit `x/1` | Insert `2+` before denominator `1`, submit | Result is `x/(2+1)`. |
| FRA-06 | Commit `(x+1)/1` | Delete `+1` from numerator, submit | Result remains one fraction with numerator `x`. |
| FRA-07 | Commit `x/(x+1)` | Replace denominator `x+1` with `x-1`, submit | Only the denominator changes. |
| FRA-08 | Commit `(x+1)/(x-1)` | Add `2*` before numerator, submit | Result is `(2*x+1)/(x-1)` or the visually equivalent Windows grouping. |
| FRA-09 | Commit `(x+1)/(x-1)` | Add `+2` after the denominator, but inside it, submit | Result is `(x+1)/(x-1+2)`. |
| FRA-10 | Commit `(x+1)/(x-1)` | Add `+2` after the complete fraction, submit | Result is `((x+1)/(x-1))+2`. |
| FRA-11 | Commit `(x/2)/3` | Add `+1` to the innermost numerator `x`, submit | Both fraction levels remain intact. |
| FRA-12 | Commit `x/(1/2)` | Add `+1` to the inner numerator `1`, submit | Result is `x/((1+1)/2)`. |
| FRA-13 | Commit `(x+1)/(2+3)` | Click repeatedly around both sides of the fraction bar | Record every distinct caret landing position and which structure receives typed `9`. Undo after each probe. |
| FRA-14 | Commit `1/x` | Replace numerator `1` with `x+1`, submit | Result is `(x+1)/x`. |
| FRA-15 | Commit `x/1` | Select numerator `x`, type `x+1`, submit | Replacement remains the numerator. |
| FRA-16 | Commit `x/1` | Select the whole fraction, type `x+1`, submit | The fraction is replaced rather than partially retained. |
| FRA-17 | Commit `x/1` | Type `/2` while caret is inside numerator | Record whether Windows creates a nested numerator fraction or exits the current fraction. |
| FRA-18 | Commit `x/1` | Type `/2` while caret is inside denominator | Record the exact nested structure. |
| FRA-19 | Commit `x/1` | Type `/2` immediately after the complete fraction | Record whether the result is `(x/1)/2` and how it is visually nested. |
| FRA-20 | Commit `x/0` | Edit denominator to `1`, submit | Error state clears and the graph reappears without changing the numerator. |

## C. Powers, roots, and scripts

| ID | Setup | Action | Expected invariant |
|---|---|---|---|
| POW-01 | Commit `x^2` | Append `+1` inside exponent, submit | Result is `x^(2+1)`. |
| POW-02 | Commit `x^2` | Type `+1` after the complete power, submit | Result is `x^2+1`. |
| POW-03 | Commit `x^2` | Insert `2*` before base `x`, submit | Record whether exponent applies to `x` only or to the edited base group. |
| POW-04 | Commit `(x+1)^2` | Add `+1` inside the base, submit | Exponent remains attached to the entire parenthesized base. |
| POW-05 | Commit `(x/2)^3` | Add `+1` to fraction numerator, submit | Result is `((x+1)/2)^3`. |
| POW-06 | Commit `x^(1/2)` | Add `+1` to exponent numerator, submit | Result remains a fractional exponent with numerator `1+1`. |
| POW-07 | Commit `x^(2^3)` | Edit inner exponent `3` to `3+1`, submit | Both exponent levels remain intact. |
| POW-08 | Commit `sqrt(x)` | Add `+1` inside radicand, submit | Result is `sqrt(x+1)`. |
| POW-09 | Commit `cbrt(x)` | Add `+1` inside radicand, submit | Result is `cbrt(x+1)`. |
| POW-10 | Commit `root(x,3)` using the keypad | Edit the radicand, then the degree | Record pointer and arrow-key access to both structural slots. |
| POW-11 | Commit `10^x` | Add `+1` to exponent, submit | Result is `10^(x+1)`. |
| POW-12 | Commit `x^2` | Backspace directly after the superscript group twice | Record selection-preview and deletion behavior for the exponent. |

## D. Functions and delimiters

| ID | Setup | Action | Expected invariant |
|---|---|---|---|
| FUN-01 | Commit `sin(x)` | Add `+1` to argument, submit | Result is `sin(x+1)`. |
| FUN-02 | Commit `sin(x)` | Add `+1` after the closing parenthesis, submit | Result is `sin(x)+1`. |
| FUN-03 | Commit `sin(x)` | Delete the closing parenthesis, submit | Record error, auto-repair, and visual delimiter behavior. |
| FUN-04 | Commit `sin(x)` | Delete `sin` but leave `(x)`, submit | Record whether the remaining group becomes ordinary parentheses. |
| FUN-05 | Commit `abs(x)` | Add `-1` inside the argument, submit | Result is `abs(x-1)`. |
| FUN-06 | Commit `log(x)` | Add `+1` inside the argument, submit | Result is `log(x+1)`. |
| FUN-07 | Insert log-base-Y from keypad | Edit base and argument independently | Record initial caret/selection and the final structured layout. |
| FUN-08 | Commit `floor(x)` | Add `+1` inside | Result is `floor(x+1)`; record delimiter glyphs during editing. |
| FUN-09 | Commit `ceiling(x)` | Add `+1` inside | Result is `ceiling(x+1)`; record delimiter glyphs during editing. |
| FUN-10 | Commit `sin(cos(x))` | Add `+1` to innermost argument | Both function boundaries remain intact. |
| FUN-11 | Commit `(x+1)*(x-1)` | Insert text beside each of the four parentheses | Record which clicks land inside versus outside each group. |
| FUN-12 | Commit `f(x)=x^2` if accepted | Edit the function argument and right side separately | Record any special treatment of the function label. |

## E. Caret and navigation

| ID | Setup | Action | Observation to record |
|---|---|---|---|
| NAV-01 | Commit `x/1` | Press Left repeatedly from the end | Record the ordered caret path through denominator, numerator, and outside positions. |
| NAV-02 | Commit `x/1` | Press Right repeatedly from the start | Record the ordered caret path. |
| NAV-03 | Commit `x/1` | From denominator, press Up and Down | Record whether vertical navigation switches fraction slots. |
| NAV-04 | Commit `x^2` | Press Left, Right, Up, and Down around the exponent | Record structural transitions. |
| NAV-05 | Commit `(x+1)/(x-1)` | Press Home and End from each fraction slot | Record whether these keys target the slot, equation, or text line. |
| NAV-06 | Commit a long expression wider than the row | Move caret from start to end | Record horizontal scrolling and whether the caret remains visible. |
| NAV-07 | Commit `x/1` | Click directly on fraction bar | Record the chosen slot and caret position. |
| NAV-08 | Commit `x/1` | Click just left, right, above, and below the fraction | Record hit-testing boundaries. |
| NAV-09 | Commit `(x/2)^3` | Shift+Arrow through every structure | Record selection granularity and direction. |
| NAV-10 | Commit `sin(x/2)` | Ctrl+Left and Ctrl+Right | Record whether movement is lexical, structural, or unsupported. |

## F. Backspace, Delete, selection, and undo

The Windows source deliberately gives complex groups a deletion preview:
Backspace may select a group on the first press and delete it on the second.

| ID | Setup | Action | Expected or observation |
|---|---|---|---|
| DEL-01 | Empty row | Press Backspace and Delete | No crash, row removal, or graph change. |
| DEL-02 | Commit `x+1` | Backspace after `1` | Single character is deleted immediately. |
| DEL-03 | Commit `x/1` | Backspace after complete fraction, then Backspace again | Record first-press selection and second-press deletion. |
| DEL-04 | Commit `x^2` | Backspace after exponent group twice | Record preview granularity. |
| DEL-05 | Commit `sin(x)` | Backspace after function group twice | Record whether function and argument are one deletion unit. |
| DEL-06 | Commit `(x+1)` | Backspace beside closing parenthesis | Record whether delimiter, group, or final character is selected/deleted. |
| DEL-07 | Commit `x/1` | Delete forward immediately before complete fraction | Record whether behavior mirrors Backspace. |
| DEL-08 | Commit `x/1` | Select only numerator and press Backspace | Only selected numerator content is removed. |
| DEL-09 | Commit `x/1` | Select denominator and type `2` | Selection is replaced in place. |
| DEL-10 | Commit `(x+1)/(x-1)` | Select across the fraction boundary | Record whether partial cross-structure selection is permitted. |
| DEL-11 | Perform DEL-03 | Press Ctrl+Z once and twice | Record each restored structural state. |
| DEL-12 | Edit and submit an equation | Press Ctrl+Z while still focused | Record whether undo crosses the submission/formatting boundary. |
| DEL-13 | Edit, submit, focus again | Press Ctrl+Z | Record whether undo history survives focus loss. |
| DEL-14 | Commit `x/1` | Ctrl+A, type `x+1` | Entire equation is replaced cleanly. |

## G. Incomplete and invalid expressions

| ID | Input or setup | Action | Observation to record |
|---|---|---|---|
| ERR-01 | `x+` | Press Enter | Error message, underline/highlight, retained text, caret, and graph behavior. |
| ERR-02 | `x/` | Press Enter | Whether an empty denominator placeholder remains structurally visible. |
| ERR-03 | `/x` | Press Enter | Whether an empty numerator placeholder remains visible. |
| ERR-04 | `x^` | Press Enter | Whether an empty exponent placeholder remains visible. |
| ERR-05 | `sqrt(` | Press Enter | Delimiter repair versus error behavior. |
| ERR-06 | `(x+1` | Press Enter | Auto-closing versus unmatched-parenthesis error. |
| ERR-07 | `x+1)` | Press Enter | Extra-closing-parenthesis behavior. |
| ERR-08 | `x//1` | Press Enter | Nested fraction, normalization, or error behavior. |
| ERR-09 | `x^^2` | Press Enter | Nested exponent versus error behavior. |
| ERR-10 | `1/0` | Press Enter | Parse success versus graph/evaluation error. |
| ERR-11 | `0/0` | Press Enter | Error category and retained structured presentation. |
| ERR-12 | `unknown(x)` | Press Enter | Unknown-function error and formatting. |
| ERR-13 | `y=x+` | Press Enter | Error location and whether the equality remains structured. |
| ERR-14 | Begin with ERR-01 | Add `1`, press Enter | Error clears and expression reparses without losing the earlier draft. |
| ERR-15 | Begin with ERR-02 | Enter denominator `1`, press Enter | Fraction recovers without moving `x` outside the numerator. |
| ERR-16 | Begin with ERR-04 | Enter exponent `2`, press Enter | Power recovers without detaching the exponent. |
| ERR-17 | Invalid committed row | Click a different equation and return | Record draft, formatting, and error-state persistence. |

## H. On-screen graph keypad

| ID | Action | Expected or observation |
|---|---|---|
| KEY-01 | On an empty row, press `x`, Divide, `1`, Submit | Produces and submits `x/1`; record the caret path created by the buttons. |
| KEY-02 | Insert `sin` from the Trigonometry menu | Windows inserts `sin()` and places the caret inside; verify exact selection/caret. |
| KEY-03 | Insert `sqrt` | Windows inserts `sqrt()` and places the caret inside; verify formatting before submission. |
| KEY-04 | Insert general root | Record inserted template, selected placeholder, list separator, and caret offset. |
| KEY-05 | Insert log-base-Y | Record base/argument placeholders and initial selection. |
| KEY-06 | Insert absolute value | Record whether it displays as `abs()`, vertical bars, or another math form while editing. |
| KEY-07 | With caret in a numerator, press keypad `+`, `1`, `1` | All tokens stay in the numerator. |
| KEY-08 | With caret in an exponent, press keypad `+`, `1` | Both tokens stay in the exponent. |
| KEY-09 | Press keypad Backspace beside a complex fraction twice | Compare with physical Backspace behavior. |
| KEY-10 | Press keypad Clear on a committed equation | Record whether the row remains allocated, whether a new row is created, and where focus lands. |
| KEY-11 | Click the gap between keypad buttons | **PASS** if equation focus and caret do not move. |
| KEY-12 | Open and dismiss a keypad flyout | Record whether equation focus, selection, and draft remain intact. |

## I. Clipboard and text services

| ID | Setup | Action | Observation to record |
|---|---|---|---|
| CLP-01 | Commit `x/1` | Ctrl+A, Ctrl+C, paste into Notepad | Record whether clipboard text is linear text, UnicodeMath, MathML, RTF, or multiple formats. |
| CLP-02 | Commit `x/1` | Select numerator only, copy, paste into a new equation | Record retained structure and formatting. |
| CLP-03 | Commit `x/1` | Cut numerator, then paste it back | Fraction boundary must survive both operations. |
| CLP-04 | New row | Paste plain text `(x+11)/1`, submit | Result is a single fraction with numerator `x+11`. |
| CLP-05 | New row | Paste plain text `x+11/1`, submit | Record the precedence Windows chooses. |
| CLP-06 | New row | Paste `x/1` copied from the formatted Windows equation | Compare with CLP-04 and direct typing. |
| CLP-07 | New row | Paste multiline text `x\n+1` | Record newline rejection, normalization, or truncation. |
| CLP-08 | New row | Paste Unicode `π`, `θ`, `×`, `÷`, `−`, `≤`, `≥` | Record accepted symbols and canonical formatting. |
| CLP-09 | New row | Enter text using an IME | Record composition underline, candidate selection, commit, and math formatting. |
| CLP-10 | New row | Use handwriting or touch keyboard math input if available | Record whether input enters the math zone and survives submission. |

## J. Typography and live-formatting observations

Capture 200% or greater crops for these cases.

| ID | Setup | Action | Observation to record |
|---|---|---|---|
| TYP-01 | Empty row | Type `x`; screenshot focused and after Enter | Compare exact glyph, italic angle, baseline, advance width, size, and antialiasing. |
| TYP-02 | Empty row | Type `x+1`; screenshot before and after Enter | Record which characters change shape or spacing on submission. |
| TYP-03 | Empty row | Type `sin(x)` slowly | Record when `sin` becomes upright and when parentheses become structured. |
| TYP-04 | Commit `x/1` | Insert `+11` in numerator but do not submit | Record whether inserted characters are immediately math-formatted or temporarily plain runs. |
| TYP-05 | Continue TYP-04 | Press Enter | Record every visual change caused by final formatting. |
| TYP-06 | Commit `x^2` | Insert `+1` in exponent | Compare font size and baseline of old and newly inserted exponent characters. |
| TYP-07 | Commit `(x+1)/(x-1)` | Move caret through all slots | Record caret height, vertical position, thickness, blink, and clipping. |
| TYP-08 | Trigger a parse error | Compare normal, focused, error, and selected text | Record foreground, underline, background, and selection colors. |
| TYP-09 | Repeat TYP-01 in Light, Dark, and High Contrast | Compare glyph metrics and only allow color/antialiasing changes | Record theme-specific differences. |
| TYP-10 | Repeat TYP-01 at 100%, 125%, 150%, and 200% scaling | Record rounding, clipping, and baseline stability. |

## K. Locale and limits

| ID | Setup or action | Observation to record |
|---|---|---|
| LOC-01 | Use a decimal-comma locale and enter `1,5*x` | Decimal interpretation and formatted output. |
| LOC-02 | In the same locale, insert a multi-argument root or log template | Exact list separator and initial selection. |
| LOC-03 | Switch keyboard layout while an equation is focused | Whether caret, draft, and shortcuts remain stable. |
| LOC-04 | Enter localized digits if the keyboard supports them | Accepted digits and canonical form. |
| LIM-01 | Paste 2047 characters | Responsiveness, horizontal scrolling, and submission. |
| LIM-02 | Attempt to enter beyond 2048 characters | Exact truncation/rejection behavior and caret position. |
| LIM-03 | Create 20 nested parentheses | Formatting latency, caret navigation, and submission. |
| LIM-04 | Create 10 nested fractions | Layout, row height, clipping, caret navigation, and deletion. |
| LIM-05 | Hold a digit key for key repeat | Missing, duplicated, or reordered input. |
| LIM-06 | Rapidly alternate physical keys and keypad buttons | Focus retention and token ordering. |
| LIM-07 | Resize the window while editing a wide fraction | Caret visibility, scrolling, clipping, and row reflow. |

## L. Graph and row-state integration

| ID | Setup | Action | Expected or observation |
|---|---|---|---|
| INT-01 | Commit `x`, note graph | Edit to `x+1` without submitting | Record whether graph updates live or only after submission. |
| INT-02 | Continue INT-01 | Press Enter | Graph and equation presentation update to the same expression. |
| INT-03 | Two equations, `x` and `x/1` | Edit only the second numerator | First equation, color, style, enabled state, and graph remain unchanged. |
| INT-04 | Commit an invalid edit | Correct it and submit | Original color/style survive error recovery. |
| INT-05 | Commit `x`, then clear it | Record row allocation, function numbering, color reuse, and focus. |
| INT-06 | Commit an equation in the last row | Record creation and focus behavior of the new placeholder row. |
| INT-07 | Edit a middle row and press Enter | Record whether focus stays, advances, or creates any row. |
| INT-08 | Hide an equation, edit it, submit | Record whether enabled state is preserved and whether graph remains hidden. |
| INT-09 | Change line style, then edit and submit | Style and width remain attached to the same row. |
| INT-10 | Begin tracing, then focus and edit an equation | Record tracing cancellation, focus, graph refresh, and cursor behavior. |

## Suggested result log

Copy this table for each testing session:

| Test ID | Result | Actual Windows behavior | Screenshot/video | Difference from Avalonia | Follow-up |
|---|---|---|---|---|---|
| | | | | | |

## Source-derived behaviors

These expectations come directly from the pinned Windows Calculator source:

- A single `MathRichEditBox` is placed in math-only RichEdit mode.
- Main Enter submission occurs on key-up.
- Losing focus submits unless the editor is read-only or its context flyout is
  open.
- Submission asks the graph control to format the complete math expression
  before updating the bound value.
- Backspace deletes a single character immediately, but a complex preceding
  group can be selected first so the user can preview a second-press deletion.
- Keypad functions insert templates such as `sin()`, `sqrt()`, `root(x,n)`,
  and `log(b,x)` with explicit caret offsets and selections.
- The edit control is single-line, non-wrapping, and limited to 2048
  characters.

Relevant pinned source files:

- `upstream/windows-calculator/src/Calculator/Controls/MathRichEditBox.cs`
- `upstream/windows-calculator/src/Calculator/Controls/EquationTextBox.cs`
- `upstream/windows-calculator/src/Calculator/Views/GraphingCalculator/GraphingNumPad.xaml.cs`
- `upstream/windows-calculator/src/Calculator/Views/GraphingCalculator/EquationInputArea.xaml.cs`
- `upstream/windows-calculator/src/Calculator/Views/GraphingCalculator/GraphingCalculator.xaml.cs`

