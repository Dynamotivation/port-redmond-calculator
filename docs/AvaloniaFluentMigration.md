# Windows XAML Fluent to Avalonia Fluent migration guide

This guide records reusable behavior differences encountered when porting a
Fluent WPF, UWP, or WinUI application to Avalonia. It is a framework migration guide,
not an application changelog or a list of pending work.

Avalonia Fluent is a Fluent-inspired theme, not a template-compatible or
pixel-equivalent implementation of Windows Fluent. A successful port preserves the source
application's visual states, layout contracts, composition, accessibility, and
interaction semantics instead of merely translating XAML namespaces.

## Classify differences before overriding them

Every discrepancy should be classified as one of the following:

1. a UWP/WinUI default versus an Avalonia Fluent default;
2. an application-owned source template that must be ported explicitly;
3. a host-platform windowing or composition difference; or
4. a renderer difference that requires perceptual rather than numeric parity.

Read the source style's complete inheritance chain. A custom UWP template can
still inherit brushes, borders, elevation, minimum sizes, and state resources
from `DefaultButtonStyle`. Copying only its local setters silently drops those
inherited behaviors.

### Port semantics, not toolkit numbers

Do not copy dimensions from a visually correct implementation in another UI
toolkit and assume Avalonia will render them identically. Padding, border
placement, antialiasing, device-pixel snapping, default minimums, and shadow
kernels differ between renderers. A fractional margin in one toolkit can need a
larger Avalonia value to produce the same physical gap.

Use another implementation to identify the intended color, state, radius,
stroke, and elevation contract. Then tune Avalonia values against rendered
output. Record both the source intent and the Avalonia compensation so later
maintainers do not “correct” a deliberate numerical difference.

## Buttons and owned templates

| Behavior | UWP/WinUI behavior | Common Avalonia difference | Migration rule |
|---|---|---|---|
| Geometry | Application-owned controls often stretch in a grid with deliberate minimums, margins, corner radius, and centered content. | A stock Avalonia `Button` retains its own padding, minimums, border, and presenter geometry. | For fidelity-critical controls, own the template and make sizing responsibilities explicit. Let the parent grid own proportional width. |
| Perceived elevation | A custom source template can inherit a gradient stroke, shadow, or elevation from its base style even when it locally replaces the fill. Some Light-mode controls use no shadow at all: a one-pixel light bezel surrounds the control and its lower pixels transition to a darker stroke. | An owned Avalonia template receives none of that treatment automatically. Copying a generic shadow, a full-height top-to-bottom gradient, or a uniform two-pixel border makes the control too dark or reduces its apparent face on every edge. | Inspect the fully resolved visual result, not one property. For a bottom-weighted bezel, keep a uniform one-pixel border, hold the upper and side color constant through most of the gradient, place the darker stop near the bottom, and omit `BoxShadow`. Recreate pressed treatment separately only when the source changes it. Compare the rendered edge at target scale and tune fractional control margins independently of border thickness. |
| Key spacing | Source margins and renderer antialiasing combine to produce the apparent gap. | Updating only horizontal margins leaves row spacing inconsistent. Copying a fractional value from another renderer can halve the apparent Avalonia gap. | Audit all four sides and tune against physical output. Use equal four-sided spacing when the reference grid is symmetric, even when Avalonia needs a different numeric value to achieve it. |
| Hover and press | Visual states target named template elements with explicit brushes. | Avalonia pseudo-classes can combine with Fluent template layers and override a locally assigned `Background`. | Assign normal, `:pointerover`, and `:pressed` states to the owned surface rather than stacking color setters onto an unknown template. |
| Accent buttons | Emphasized controls remain accent-colored during hover and press. | A stock pointer-over layer can replace the accent fill. | Define explicit accent normal, hover, and pressed resources on the owned surface. |
| Mixed opaque and material keys | Primary entry controls can be fully opaque while secondary commands use the same backdrop-compatible material system as panes and cards. A documented Mica-off surface such as `#F9F9F9`, with `#F6F6F6` on hover, describes the final opaque fallback—not necessarily the tint RGB used by the translucent material. | Applying one shared button fill makes every key opaque. Merely adding alpha to the fallback color also darkens its composited result; for example, 50% `#F9F9F9` over `#F3F3F3` resolves near `#F6F6F6`. Setting `Opacity` on the whole control additionally fades text and icons. | Keep separate opaque fallback and translucent material resources for default, hover, pressed, bezel, and shadow states. To preserve an intended final color over a known fallback, solve the alpha-compositing equation for the material tint; at 50% over `#F3F3F3`, final `#F9F9F9` needs approximately a white tint, while final `#F6F6F6` needs approximately `#F9F9F9`. Apply alpha to surface brushes, never the whole control. Classify sign and decimal controls by their entry role rather than glyph shape. |
| Disabled controls | Source templates can keep a transparent background while changing only foreground and opacity. | The stock disabled template may draw a rounded fill and alter apparent scale. | Explicitly define disabled background, border, foreground, and opacity. |
| Small responsive buttons | Parent grids can enforce a fixed ratio to larger input controls. | Default minimum width and padding can defeat the ratio. | Set `MinWidth=0`, `MinHeight=0`, zero padding, stretch alignment, and let the grid define width. |
| Button typography | Caption/memory labels and large numeric-entry glyphs can look similarly heavy while resolving from different weights. | Assigning one replacement weight globally can make already-Regular numeric keys heavier while correctly reducing captions. | Inspect each class's resolved weight and compare visible strokes, not weight names. Reduce each independently—for example, memory captions and Regular numeric keys may both require Light on the target—while leaving operators and accent actions on their own typography contract. |
| Brush transitions | WinUI templates may specify short `BrushTransition` durations. | Avalonia property changes are immediate unless a transition is declared. | Port timing and easing when the transition is visually material. |
| Template state targeting | WinUI visual states usually target named elements directly. | Avalonia selector specificity and declaration order can let generic rules beat specialized rules. | Name important template surfaces and target them through `/template/`. Order selected and disabled overrides deliberately. |

### Transparent is not the same as visually inert

Setting `Background="Transparent"` does not remove interaction layers inside a
stock Fluent template. Use a minimal owned template when a button must remain
visually transparent in every state.

Conversely, replacing a template solely to remove its resting fill can also
remove legitimate hover and pressed feedback. Preserve the stock presenter when
those states are desired and tune its resources instead.

Invisible full-window hit targets require an owned transparent template. A
stock transparent button can still draw a pressed layer and flash the entire
window.

## Color compositing

When a reference specifies the final composited color, calculate the overlay
instead of guessing an opaque replacement. For source-over blending:

```text
result = overlay × alpha + background × (1 - alpha)
```

For example, black at alpha `9/255` over `#F3F3F3` rounds to `#EAEAEA`.
Represent that as `#09000000`. If selected and hovered rows use the same visual
fill, do not stack the hover overlay over the selected fill; the result would be
darker than either intended state.

Use explicit secondary-foreground brushes when an exact color is required.
Applying opacity to inherited foreground changes the result with every surface
behind the text.

## Navigation drawers

Avalonia does not provide a template-compatible replacement for WinUI
`NavigationView`. Recreate its layout, state, motion, and accessibility as a
coordinated shell.

| Behavior | Migration rule |
|---|---|
| Overlay mode | Keep the drawer separate from page layout so opening it does not resize the active page. |
| Toggle layering | Keep one toggle later in the visual tree than the moving pane so the pane slides underneath it. |
| Toggle hitbox | Give the navigation toggle its own near-square hitbox; some Fluent layouts make it roughly 10% wider than high. Do not inherit dimensions from nearby history, compact-overlay, back, or caption controls merely because they all appear in chrome-adjacent areas. Size those other in-content chrome actions independently, commonly with a smaller square hitbox. |
| Scrollbar clearance | Fluent scrollbar templates can place generated bars closer to the content or viewport edge than the source design. Style the `ScrollBar` template parts produced by `ScrollViewer`, rather than adding unrelated padding to every page. Verify vertical and horizontal bars independently because a global margin affects both axes. |
| Re-entry | Track transition state separately from destination state and disable the toggle until motion completes. |
| Motion | Keep the pane mounted and animate margin or translation with the source duration and easing. Visibility changes alone snap. |
| Light dismiss | Use an invisible hit target only when the reference has no modal scrim. Give it a truly transparent template. |
| Footer | Keep scrolling navigation content separate from a fixed footer. Draw a real one-pixel separator instead of using text or a disabled control as a divider. |
| Navigation rhythm | Treat row spacing, section-label typography, post-list clearance, and footer-divider width as independent metrics. Increase item margins without changing hitbox height; let a full-width footer separator reach both pane edges; and tune the final list margin separately from the fixed footer gap. |
| Selection | Store selection explicitly. Keep hover, press, selected fill, and accent indicator as separate visual concepts. |
| Disabled rows | Preserve source order and suppress hover fills when disabled. |

### Translucent panes must not reveal covered controls

A translucent Avalonia pane normally blends with controls already rendered
behind it. Acrylic-like composition should sample the backdrop without exposing
covered page content.

Place page content and the pane in separate layers. Animate a clip or culling
boundary on the page content in lockstep with the moving pane edge. Remove the
clip entirely when its width returns to zero so the closed state has no ongoing
masking cost. Draw the translucent pane over the host's backdrop layer after
the covered page region has been culled.

## Page hosting

UWP navigation replaces the active page beneath shared window chrome. Do not
simulate navigation by placing a transparent page over the previous page.
Doing so leaks old visuals through material surfaces and leaves inactive
controls in layout and hit testing.

Use mutually exclusive sibling page containers. Keep only genuinely shared
chrome outside those containers.

## Settings cards and expanders

Toolkit `SettingsCard` and `SettingsExpander` controls are not equivalent to a
plain Avalonia `Expander`. Their surface ownership, spacing, separator, and
motion need deliberate templates.

| Behavior | Common failure | Migration rule |
|---|---|---|
| Stretching | The header sizes to content and leaves unused space at the right. | Set both `HorizontalAlignment` and `HorizontalContentAlignment` to `Stretch`. |
| Double bezel | An outer card, header `ToggleButton`, and `ExpanderContent` each draw a slightly different surface. | Choose one owner for fill, border, radius, and shadow. Clear nested header/content borders and padding. |
| Material ownership | Applying translucent fill to both an outer card and its inner header double-stacks opacity. | Give the fixed header and animated content host one material layer each; leave the enclosing fill transparent. Apply the same material policy to non-expanding cards. |
| Bezel composition | An opaque one-pixel border remains visually detached from a translucent card. | Preserve the specified RGB but apply the material alpha to the stroke and proportionally reduce inner/outer fade alpha. |
| Chevron interaction | The stock chevron draws its own rounded hover and pressed backplate. | Make chevron backplates transparent while preserving foreground and rotation. |
| Chevron animation | Rotation can appear only in one direction. | Transition one render transform between collapsed and expanded states. |
| Expansion motion | Visibility changes reserve space immediately; content either snaps or flies over the header. | Keep the content host mounted, measure natural height, and animate both host height and content translation with cancellation-safe reversals. |
| Header layering | Moving content appears through the header during reveal. | Keep the header above the content, square its lower corners immediately, and let the rounded content surface slide from underneath. |
| Separator | A border added to header thickness changes measurement or appears only after animation. | Overlay a one-pixel separator at the fixed header wrapper's bottom edge. It must not participate in measurement. |
| Header layout shift | Expanded state changes header height by a pixel. | Use a fixed-height wrapper and vertically center an unchanged inner header grid. |
| Padding | Removing nested surfaces can leave icon and text close to the frame. | Measure visible artwork bounds. Apply balanced frame-to-icon and icon-to-text clearance, then align expanded content with the header text start. |
| Choice padding | Radio groups can have zero space above the first item and nonzero space below the last. | Give expanded choice panels matching top and bottom margins. Keep inter-item spacing independent. |
| Typography | One corrected card or page title can leave sibling headings heavier or tighter. Avalonia centers the font's ascent/descent line box, not visible glyph bounds; changing weight can reveal an apparent bias even while `VerticalAlignment="Center"` remains mathematically correct. | Audit page titles, section headings, and option headlines as one hierarchy. Encode their weight and size as shared styles. When matching a baseline-aware source against an icon, use a measured render-only translation so hitboxes and row geometry remain intact; bracket the value from rendered comparisons instead of guessing a whole-pixel offset. Keep display-output weight independent from heading weight. |
| Initial disclosure state | A section hard-coded with `IsExpanded="True"` makes the page arrive with content already reserved. More subtly, Avalonia 12.1's Fluent template binds both `ExpanderContent.IsVisible` and the header's `IsChecked` to `IsExpanded` with `Mode=TwoWay`; animation code that forces the content host visible can therefore write `true` back and open every instance. A derived control that overrides `StyleKeyOverride` to `Expander` will also ignore a template style selected by the derived type. | Declare on-demand sections collapsed. If collapse animation requires a permanently mounted host, own the Expander template under the effective `Expander` style key, omit the content host's visibility binding, and make header state one-way. Toggle `IsExpanded` explicitly from the header click so initialization cannot feed state back into the owner. |

## Theme resources and persistence

| Area | Migration rule |
|---|---|
| Runtime theme lookup | Use Light/Dark theme dictionaries and `DynamicResource` for custom surfaces. `StaticResource` does not reevaluate after a theme change. |
| System theme | Map the source's system/default option to `ThemeVariant.Default`. Audit high contrast separately. |
| Custom glyphs | Route strokes and fills through dynamic foreground resources. Hard-coded light glyphs become unreadable in Light mode. |
| Persistence | Keep theme preference framework-neutral. Persist it in the host's application-data location and apply it before constructing the first window. |
| Host material | Theme changes do not automatically reconfigure a native backdrop. Treat backdrop appearance as a host concern and test Light, Dark, and system changes independently. |

## Window composition and chrome

Mica is a Windows composition feature, not a portable Avalonia control.
Avalonia transparency hints can request transparency, but a true host backdrop
requires a platform adapter. Keep the managed preference independent from the
native implementation and provide an opaque fallback.

Model corner style and title-control style as separate preferences. The host
may need different implementations for the same requested controls under
different outer-window geometry. Do not rewrite one preference when the other
changes.

Keep shared appearance choices in one section, then gate only controls whose
implementation actually depends on host capabilities. A platform-named section
that also contains portable choices makes future hosts either duplicate the UI
or expose irrelevant settings.

Use native caption controls when authentic host behavior matters. Hand-drawn
imitations generally cannot reproduce hover glyphs, window menus, accessibility,
or system tiling behavior.

### Custom Windows-style caption controls

Microsoft specifies Segoe Fluent `ChromeMinimize` E921, `ChromeMaximize` E922,
`ChromeRestore` E923, `ChromeClose` E8BB, and `ChromeBack` E830. Maximize and
restore use rounded corners. Segoe Fluent Icons may not be redistributable on
every target, so use licensed portable vector geometry when necessary.

Caption controls require:

- full-bleed hover and pressed backplates;
- explicit active and inactive states;
- minimize, maximize/restore, and close semantics;
- rounded line caps and joins for modern glyph geometry;
- source-accurate button aspect ratio rather than guessed square hitboxes; and
- an owned template when theme internals override state colors.

Do not treat “Windows-style controls” as one timeless visual preset. When an
application exposes multiple Windows generations, keep each generation's
caption geometry and hitbox contract together: older chrome can use sharper,
lighter glyphs and a narrower aspect ratio, while newer chrome uses rounded,
heavier glyphs and wider targets. Bind the visual variant to the selected
title-control style—not the independently selected corner style—instead of
replacing the older assets globally.

The close hover color in current Windows guidance is `#C42B1C`.

Do not stretch a zero-height minimize path with `Stretch="Uniform"`; its empty
geometry dimension can collapse it into a dot. Render it in native coordinates
or use a shape with nonzero layout bounds.

### The one-pixel frame and full-bleed hover bug

An Avalonia root `Border` lays out children inside its `BorderThickness`. A
caption button aligned to the content edge therefore stops one pixel before the
physical window edge, leaving the root frame visible above or beside its hover
backplate.

Do not globally remove a required frame to hide this artifact. Extend caption
backplates into the frame thickness on the affected outer edges and retain the
root clip so rounded window corners still mask the fill correctly. Treat frame
thickness as part of caption geometry and verify every DPI scale.

## Icons and typography

| Area | Migration rule |
|---|---|
| Application icon font | Package redistributable source icon assets as `AvaloniaResource` and use the font's actual family name. Preserve the source's private-use glyph mapping and source font sizes; PUA code points are meaningful only with their intended font. Do not substitute visually similar Unicode operators, emoji, or platform fallback glyphs—their stroke geometry and metrics differ. |
| Stateful icon-font controls | A source control may use different private-use glyphs for enter and exit states rather than transforming one drawing. | Preserve both source code points and notify the glyph property when state changes; otherwise the command and tooltip can update while its icon continues to advertise the previous action. |
| Resource authority | Assembly-qualified `avares://` URIs change when the assembly name changes. Encode spaces and launch after every identity or asset move. |
| Default UI font | Segoe metrics differ from cross-platform fonts. Compare rendered baselines, wrapping, and control height rather than copying nominal sizes only. |
| User-selectable text fonts | Apply the selected family at a common text-bearing ancestor and persist the preference independently from theme and platform appearance. Keep semantic icon controls pinned to their licensed icon font so a text-font change cannot reinterpret private-use code points or alter operator geometry. |
| Installed-font picker | Enumerate families through the UI framework's font manager, pin the recommended default ahead of the sorted device list, persist the family name, and fall back when that family is unavailable on the next host. Preview each entry in its own family, but keep application layout usable when a font has unusual metrics. |
| Icon alignment | Fallback glyph metrics can look top-aligned. Even the intended icon font can have different line-box/baseline treatment between XAML engines, so copying the source's nominal font size may render too small and optically displaced. Use the intended font, center a fixed-size child presenter inside the unchanged hitbox, scale from rendered artwork bounds, and apply any measured optical translation to that child rather than moving the control. |
| Font licensing | A font available on Windows is not automatically redistributable to other targets. Verify licensing before embedding it; otherwise use licensed vectors. |

## Localization and binding

| Area | Migration rule |
|---|---|
| `.resw` lookup | Provide a compatibility loader that preserves culture fallback and the source key surface. |
| Regional number format | Do not infer decimal and grouping separators from UI-language resources or hard-code them in the view. Capture `CultureInfo.CurrentCulture.NumberFormat` in a framework-neutral layer, feed the same values into the calculation engine, bind keypad labels to them, and localize any subsystem that keeps canonical `.` values at its managed boundary. |
| `x:Uid` | Avalonia does not project UWP `x:Uid` properties automatically. Resolve keys explicitly or through an adapter. |
| Attached-property keys | Preserve complete suffixes and namespace-qualified property names during lookup. UWP slash paths can contain multiple property separators (`Uid/[using:Namespace]Owner/Property`); normalize every post-map separator to the dots used by `.resw`, not only the final slash, or bindings receive empty strings while popup shells still render. |
| Compiled bindings | Add `x:DataType`. For a template that deliberately reaches an ancestor command, provide a typed path or disable compiled binding only for that small scope. |
| Generated state | Computed visibility and selection properties must notify when every dependency changes. |

## Reusable Avalonia patterns

### Own fidelity-critical surfaces

```xml
<Style Selector="Button.fidelityCritical">
  <Setter Property="Template">
    <ControlTemplate>
      <Border Name="Surface"
              Background="{TemplateBinding Background}"
              CornerRadius="{TemplateBinding CornerRadius}">
        <ContentPresenter Content="{TemplateBinding Content}"
                          HorizontalContentAlignment="Center"
                          VerticalContentAlignment="Center" />
      </Border>
    </ControlTemplate>
  </Setter>
</Style>

<Style Selector="Button.fidelityCritical:pointerover /template/ Border#Surface">
  <Setter Property="Background" Value="{DynamicResource ControlHoverBrush}" />
</Style>
```

### Separate destination state from motion state

An `IsOpen` property describes the destination. A separate transition state
blocks re-entry and hit testing until motion completes. Keep cancellation and
rapid reversal behavior explicit.

### Remove dormant composition work

When a drawer closes, set its culling clip to `null` rather than retaining a
zero-width geometry. Dispose native effect objects when material is disabled.

## Verification checklist

1. Compare normal, hover, pressed, disabled, focused, selected, active, and inactive states.
2. Test minimum, reference, and wide sizes while resizing live.
3. Test Light, Dark, system theme, and high contrast where supported.
4. Inspect exact composited colors over both opaque and material backgrounds.
5. Verify shadows, borders, and full-bleed fills at multiple DPI scales.
6. Step through expansion, drawer, and theme animations frame by frame.
7. Confirm hidden pages and covered controls are absent from layout, rendering, and hit testing.
8. Test keyboard focus, access keys, screen readers, and automation names.
9. Test long localized strings and right-to-left layout.
10. Launch packaged builds on every supported host; do not rely only on a development runner.
