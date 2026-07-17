# WinUI/UWP Fluent to Avalonia Fluent migration guide

This is a living, evidence-based guide for the Redmond Calculator port. It
documents the Fluent behavior encountered while rebuilding Microsoft Windows
Calculator in Avalonia. It is exhaustive for the controls and interactions
ported so far, not a claim that every WinUI or Avalonia control has been
compared.

## Compared stack

| Windows source application | Cross-platform frontend |
|---|---|
| UWP targeting Windows SDK `10.0.26100.0` | .NET 10 desktop application |
| WinUI 2 `Microsoft.UI.Xaml` 2.8.7 | Avalonia 12.1.0 |
| `CommunityToolkit.Uwp.Controls.SettingsControls` 8.2.251219 | `Avalonia.Themes.Fluent` 12.1.0 |
| Calculator-owned XAML templates and theme resources | Calculator-owned Avalonia styles and templates |
| Segoe/Calculator Fluent icon assets | Inter plus the original `CalculatorIcons.ttf` |

The most important migration rule is that **Avalonia Fluent is a Fluent-looking
theme, not a template-compatible implementation of WinUI**. Matching a WinUI
application requires porting the source application's intent and visual states,
not merely changing XAML namespaces and relying on Avalonia defaults.

The Windows application itself also overrides WinUI extensively. A difference
must therefore be classified as one of:

1. a WinUI-versus-Avalonia default difference;
2. a Calculator-specific WinUI template that has not yet been ported; or
3. a native-platform difference such as window chrome or backdrop composition.

## Current source-of-truth files

- WinUI resource overrides and Calculator control templates:
  [`src/Calculator/App.xaml`](../src/Calculator/App.xaml)
- WinUI navigation shell:
  [`src/Calculator/Views/MainPage.xaml`](../src/Calculator/Views/MainPage.xaml)
- WinUI settings surface:
  [`src/Calculator/Views/Settings.xaml`](../src/Calculator/Views/Settings.xaml)
- WinUI calculator layout:
  [`src/Calculator/Views/Calculator.xaml`](../src/Calculator/Views/Calculator.xaml)
- Avalonia shell and migrated templates:
  [`src/Frontends/Calculator.Avalonia/MainWindow.axaml`](../src/Frontends/Calculator.Avalonia/MainWindow.axaml)
- Avalonia native macOS backdrop:
  [`src/Frontends/Calculator.Avalonia/MacOSMicaBackdrop.cs`](../src/Frontends/Calculator.Avalonia/MacOSMicaBackdrop.cs)

## Behavior matrix

### Buttons and interaction states

| Behavior | WinUI / Calculator behavior | Avalonia Fluent 12.1 default | Migration rule used here |
|---|---|---|---|
| Calculator key geometry | Calculator replaces the default button template. Keys stretch in both axes, have `Margin=1`, `MinWidth=24`, `MinHeight=12`, centered content, and `ControlCornerRadius`. | A stock `Button` keeps Fluent padding, minimum sizing, borders, and its own content presenter geometry. | Use a dedicated `calcButton` template with an explicit border and centered presenter. Do not use a stock button with only color setters. |
| Key hover and press | `CalculatorButton` has separate `HoverBackground`, `PressBackground`, foreground, border, and disabled resources. The template applies them through visual states. | Pseudo-classes and the Fluent template can apply theme layers in addition to locally assigned `Background`. | Own the key template and assign `:pointerover` and `:pressed` brushes explicitly. |
| Background transition | Calculator's template applies an 83 ms WinUI `BrushTransition`. | A property change is immediate unless an Avalonia transition is added. | Add an Avalonia transition when the timing is visually material; do not assume Fluent supplies the WinUI duration. |
| Accent equals key | Calculator uses an accent/emphasized style whose hover and pressed states remain accent-colored. | A locally set accent background can be replaced by the stock pointer-over layer, making the key lose its accent. | Use an owned template plus explicit accent default/hover/pressed brushes. |
| Disabled small buttons | Calculator's caption template keeps the intended transparent geometry and changes disabled brushes/foreground. | The stock disabled template can draw a rounded background layer and alter apparent control scale. | Use an owned caption/memory template; make disabled background and border explicitly transparent. |
| Memory-button width | Calculator's responsive grid controls width. In the compared layout, each small memory button remains two-thirds of an input-key width. | Button minimums and padding can override grid geometry and make the content/background look too large. | Set `MinWidth=0`, `MinHeight=0`, zero padding, stretch alignment, and let the parent grid own width. |
| Hamburger hover/press | The Calculator navigation toggle has no visible rectangular hover or pressed fill in the compared Windows build. | Avalonia's Fluent `Button` draws interaction layers even when `Background=Transparent` is set locally. | Replace the toggle template with a permanently transparent presenter. A color override alone is insufficient. |
| Invisible dismiss target | WinUI NavigationView owns light-dismiss hit testing without exposing a full-window button visual. | A transparent Avalonia button still draws its Fluent pressed layer, causing a full-window white flash. | Give the dismiss button a template containing only a transparent border. |
| Navigation-row hover | In the compared Calculator pane, disabled and merely hovered entries do not gain a grey area rectangle. Selection is the persistent visual state. | Stock buttons draw hover and disabled state layers. | Own the navigation-row template. Keep hover and disabled backgrounds transparent; reserve the background and accent bar for selection. |
| Style precedence | WinUI visual states target named template elements and are mostly explicit. | Avalonia selector specificity and declaration order can allow a generic `Button:pointerover` rule to beat an intended specialized rule. | Use a dedicated class and, for fidelity-critical controls, own the template instead of stacking more color selectors. |

### NavigationView and sidebar behavior

Avalonia does not provide a drop-in equivalent of WinUI 2 `NavigationView`.
The shell behavior has to be composed from layout, transitions, hit testing,
selection state, and accessibility metadata.

| Behavior | WinUI / Calculator behavior | Avalonia implementation requirement |
|---|---|---|
| Display mode | `PaneDisplayMode="LeftMinimal"`; the closed shell shows only the pane toggle. | Build an overlay drawer rather than resizing the calculator content. |
| Pane contents | Menu items come from the authoritative `NavCategory` manifest, grouped into Calculator and Converter sections. Settings is a built-in footer entry. | Preserve source order, IDs, enabled state, localized names, and groups in frontend state. Do not derive order from the unit catalog. |
| Toggle layering | NavigationView keeps one toggle above the moving pane. The pane slides underneath it. | Keep one persistent toggle later in the visual tree than the pane; do not place a second toggle inside the drawer. |
| Toggle during motion | The toggle is not interactable while the pane is transitioning. Its hamburger glyph remains stable in the compared build. | Track an explicit transition state, block hit testing for the 220 ms motion interval, and retain the hamburger glyph. |
| Motion | NavigationView supplies pane animation. | Avalonia visibility changes are instantaneous. Keep the pane in the tree and transition its margin/translation with easing. Current match: 220 ms cubic ease-out. |
| Light dismiss | Clicking outside closes the pane without visibly tinting the Calculator content in the compared build. | Use a transparent hit target. Do not add a modal scrim color. |
| Pane surface | Calculator overrides WinUI's pane resource with `AcrylicInAppFillColorDefaultBrush`. On the reference Windows composition this reads as a solid pane over the Calculator content. | macOS behind-window blur is compositionally different and made the drawer visibly translucent. Use an opaque pane surface for the visual match; treat this as perceptual equivalence rather than literal acrylic parity. |
| Pane corners | The exposed right edge is rounded. | Apply top-right and bottom-right corner radii and clip pane content. |
| Footer divider | NavigationView supplies visual separation before Settings. | Draw a dedicated one-pixel divider. A disabled button border or glyph is not a separator. |
| Selection | WinUI owns selection state and its accent indicator. | Store selection explicitly, use a persistent selected background plus accent bar, and synchronize converter dropdown changes back to navigation state. |
| Disabled modes | WinUI can retain disabled navigation items in the manifest. | Keep them in source order, lower foreground opacity, and suppress hover/disabled area fills. |
| Pane scrolling | Menu content scrolls while Settings remains fixed in the footer. | Put only grouped items inside the scroll viewer and keep Settings in a fixed row. |

### Settings controls

The Windows page uses Community Toolkit `SettingsExpander` and `SettingsCard`,
not plain UWP `Expander` controls. Avalonia Fluent has no template-compatible
counterpart.

| Behavior | Windows Toolkit control | Avalonia Fluent default | Migration rule |
|---|---|---|---|
| Settings card | Header icon, title, description, chevron, full-width card, and expanded items are one coordinated control. | A stock `Expander` is generic and has different spacing, background layers, and presenter alignment. | Wrap the expander in a themed card and explicitly define header and content layouts. |
| Header/content width | Toolkit SettingsExpander stretches its header and expanded card content across the available width. | Avalonia's presenter can size to its content, leaving a large unused rectangle at the right. | Set both `HorizontalAlignment` and `HorizontalContentAlignment` to `Stretch`; use a transparent expander background so the outer card owns the surface. |
| Settings-card bezel | Toolkit `SettingsCard` and `SettingsExpander` expose one coordinated card surface with matching outer geometry. | Wrapping a stock Avalonia `Expander` in a card leaves two themed inner surfaces: the header `ToggleButton`, and an `ExpanderContent` border that appears only while expanded. Each is slightly smaller and owns separate fill, border, padding, and corner geometry, producing a double bezel. | Make the outer card the only surface. Scope every header-state background and border brush plus `ExpanderContentBackground` and `ExpanderContentBorderBrush` to transparent. Set header/content padding and all directional Expander content border thicknesses to zero, and remove custom header-grid margin. The outer card then owns both collapsed and expanded geometry. |
| Expander chevron interaction | Toolkit SettingsExpander keeps its chevron visually integrated into the card header. | Avalonia Fluent gives the chevron a separate rounded hover and pressed background, even after the header surface is made transparent. | Override the chevron background and border brushes to transparent for normal, hover, pressed, and disabled states. Preserve its foreground resources and rotation animation so only the unwanted interaction rectangle is removed. |
| Expander motion and layering | Toolkit SettingsExpander animates both the chevron and content in both directions. Its opaque header stays above the reveal, immediately squares its lower corners, and retains a separator while the rounded content surface slides out underneath. | Avalonia Fluent's stock chevron keyframes can be visibly asymmetric. `ContentTransition` receives the inner `Presenter`, not the `ExpanderContent` template border that owns layout. A height-only animation merely uncovers static controls. Using header `BorderThickness` for the separator both changes measurement and places the line beneath moving content. | Derive a focused Settings Expander that keeps `ExpanderContent` mounted, measures its natural height, and animates both that template part's actual `Height` and the content presenter's vertical translation with cancellation-safe reversals. Use a fixed 48-pixel wrapper for the header, center the unchanged original header grid inside it, and overlay a one-pixel separator at the wrapper's bottom edge. The wrapper owns geometry while the inner grid retains its original auto-row spacing. |
| Light Settings palette | Toolkit Settings controls use Calculator's tuned light Fluent palette. | Avalonia's generic control resources differ slightly in fill, hover, border, and foreground values. | Use `#FBFBFB` for card fill, `#F6F6F6` for the Expander header hover, `#CCCCCC` for the one-pixel bezel and separator, and `#1A1A1A` for text and icons. Add a one-pixel low-opacity inner and outer bezel blend rather than increasing the solid border thickness. |
| Inactive window material | Windows composition falls back to an inactive solid surface when the app loses activation. | A host-native material can remain visibly translucent while another app has focus, while an opaque Settings root can suppress the material even when active. | Keep the Settings root transparent so the window owns one composition layer. Cover that material with the solid surface brush while inactive (`#F3F3F3` in light mode), then transition the window background back on activation over 250 ms using Fluent's existing-element curve, `cubic-bezier(0.55,0.55,0,1)`. |
| Theme choices | Light, Dark, and system-default radio choices update the app theme. | `RequestedThemeVariant` updates stock Fluent controls, but custom hard-coded brushes remain unchanged. | Raise a framework-neutral preference event from the managed view model, map it to Avalonia `ThemeVariant`, and use theme dictionaries plus `DynamicResource` for every custom surface. |
| Theme persistence | Calculator's `ThemeHelper` stores `SelectedAppTheme` in UWP `ApplicationData.Current.LocalSettings` and restores it during startup. | Avalonia does not provide an application-settings store as part of its Fluent theme. | Persist the framework-neutral preference in the host's application-data directory and apply it before constructing the main window. The macOS/desktop host uses an atomic JSON-file replacement. |
| Platform appearance | The Windows application naturally follows Windows composition and caption conventions. | A cross-platform Avalonia window otherwise keeps the same custom Windows presentation on every host. | Expose only host capabilities that are genuinely supported. On macOS this port offers an optional backdrop, one corner-style radio group, and one title-control radio group; hide the group on other platforms. |
| Independent appearance axes | Windows owns its frame, clipping, shadow, and caption as one composition system. | A cross-platform host may need different implementations for the same requested controls under different geometry. | Model corner shape as one enum and control style as another. Persist them independently, then let the host select the compatible implementation for each combination instead of rewriting either preference. |
| Radio binding | UWP handlers update app state after `SelectionChanged`. | A normal `IsChecked` binding may default to two-way and try to write into a computed read-only property. | Bind computed checked state explicitly one-way and send selection through a command. |
| Page hosting | UWP navigation replaces the active page beneath shared window chrome. | A transparent Settings overlay leaves the Calculator page visible and interactive underneath it. | Put Calculator and Settings in mutually exclusive sibling page containers, and keep only the title bar shared. Do not use an opaque page background to disguise an overlay architecture. |
| Back navigation | The Windows Settings page is a full-page popup with a source-localized back button. | Avalonia has no automatic Settings navigation stack in this shell. | Model `IsSettingsOpen`, hide the pane toggle, expose a back command, and retain window chrome separately. |
| About links | WinUI `HyperlinkButton` launches platform URIs. | Avalonia requires the top-level launcher or host-specific navigation. | Use `TopLevel.Launcher`; replace Windows-only schemes with meaningful cross-platform HTTPS destinations. |

### Theme resources

| Behavior | WinUI / UWP | Avalonia |
|---|---|---|
| Theme lookup | Calculator uses `ThemeResource`, so custom brushes are reevaluated when the requested theme changes. | `StaticResource` resolves once. `DynamicResource` is needed for runtime theme updates. | Put custom colors in Light/Dark theme dictionaries and reference them dynamically. |
| Default versus system theme | UWP distinguishes Default, Light, and HighContrast dictionaries. | Avalonia uses `ThemeVariant.Default`, `Light`, and `Dark`; platform high contrast needs separate auditing. | Map “Use system setting” to `ThemeVariant.Default`. Do not claim High Contrast parity until tested on every host. |
| Custom foreground | WinUI theme resources update icon/text foregrounds together. | Hard-coded white path strokes remain white in Light mode even when text switches to black. | Route custom icon strokes and title-bar glyphs through a dynamic chrome-foreground brush. |
| Mica and theme | WinUI's backdrop and theme resources are coordinated by Windows composition. | Changing Avalonia theme does not automatically reconfigure a native `NSVisualEffectView`. | Treat native backdrop appearance as a separate host concern and test Light, Dark, and system changes on macOS. |

### Backdrop, window, and application identity

| Area | WinUI/UWP behavior | Avalonia/macOS behavior and port rule |
|---|---|---|
| Mica | `BackdropMaterial.ApplyToRootOrPageBackground` integrates with Windows composition. | Avalonia has transparency hints but no cross-platform WinUI Mica implementation. The macOS host inserts an `NSVisualEffectView` behind Avalonia content. |
| Optional backdrop | Mica is part of the Calculator's Windows visual identity. | Users may prefer an opaque native-app surface for performance, contrast, or personal preference. | Dispose the native effect view immediately when disabled and switch the Avalonia root to an opaque themed brush; recreate it when enabled. |
| Rounded backdrop | Windows composition clips with the app window. | A native effect view does not inherit Avalonia border clipping. In the custom Windows-frame mode, set the native layer corner radius and clip the Avalonia root. In native-geometry modes, use a zero inner radius and let AppKit own the outer mask and shadow. |
| Drag region | Windows title-bar integration provides drag behavior around caption controls. | A borderless Avalonia window needs explicit `BeginMoveDrag`; bind the complete intended title region, not only the app icon. |
| Resizing | Native Windows chrome supplies resize hit tests. | `WindowDecorations=None` requires explicit edge and corner hit targets calling `BeginResizeDrag`. |
| Caption buttons | Windows supplies platform caption semantics and state visuals. | Custom Avalonia buttons need minimize, maximize/restore, close handlers and deliberate hover treatment. |
| Native macOS geometry | Not applicable to the Windows source application. | `WindowDecorations=BorderOnly` plus `ExtendClientAreaToDecorationsHint=true` gives macOS ownership of the outer mask, shadow, and resizing while Calculator continues to draw its Windows caption row. Remove the inner border/radius and custom resize targets in this mode. |
| Native macOS controls | Not applicable to the Windows source application. | Use three host paths. Windows controls remain Avalonia chrome. macOS controls with Windows geometry use standalone buttons from `+[NSWindow standardWindowButton:forStyleMask:]`. The macOS-controls plus macOS-corners combination uses `WindowDecorations=Full`, a transparent full-size title bar, and AppKit's window-owned buttons so hover glyphs and window-management menus remain authentic. |
| Corner-style choices | Windows 11 Calculator uses rounded corners; Windows 10 uses square corners. | A borderless transparent window must establish its own clip before data binding and ask AppKit to recalculate its shadow after geometry changes. | Use an eight-pixel custom radius for Windows 11 and zero for Windows 10. Select `BorderOnly` extended-client geometry for macOS corners. Rebuild backdrop clipping, resize handles, and native controls after a geometry transition. |
| Native-to-custom transition | Windows composition changes frame state as one operation. | A direct change from the full AppKit title bar to Windows 11 geometry while simultaneously inserting standalone AppKit buttons can retain the old frame surface. The equivalent two-step user transition through macOS geometry with Windows controls is stable. | Mirror the stable sequence internally: change `Full` to `BorderOnly`, then on the next dispatcher turn change `BorderOnly` to the custom Windows frame and attach standalone controls. Keep the persisted selections unchanged and guard deferred work against newer preference changes. |
| Visible product name | The compared Calculator UI does not need to repeat the fork name in its content. | Keep `Redmond Calculator` as window/bundle metadata while avoiding duplicate visible labels in title and drawer content. |
| Dock/process label | Bundle display name does not rename the executable launched by `dotnet run`. | Set `AssemblyName`/app-host name as well as `CFBundleDisplayName`, `CFBundleName`, and `CFBundleExecutable`. Preserve `RootNamespace` so C# namespaces do not change. |
| Assembly resource URI | WinUI uses `ms-appx:///` package URIs. | Avalonia uses `avares://Assembly/…`; renaming the assembly invalidates embedded font/image URIs. Encode spaces and update every assembly-qualified URI. |

### Icons and typography

| Area | Windows behavior | Avalonia migration rule |
|---|---|---|
| Navigation and calculator glyphs | Calculator ships `Assets/CalculatorIcons.ttf` and references its exact glyph values. | Package the same font as an `AvaloniaResource` and use its actual family name. Do not substitute Unicode emoji or platform symbols. |
| Missing font | UWP package URIs resolve against the application package. | An invalid Avalonia resource authority can compile successfully and fail only during glyph measurement at runtime. Always launch after renaming an assembly or moving assets. |
| Default UI font | Windows Fluent typography is designed around Segoe UI. | Avalonia's cross-platform setup here uses Inter, so text metrics, baselines, and wrapping differ slightly even at the same nominal size. Audit layout from rendered output rather than copying only font sizes. |
| Icon alignment | WinUI templates center icon presenters according to their own metrics. | Stock Avalonia content presenters and fallback glyph metrics can make icons appear top-aligned. Use the original font and explicit horizontal/vertical content alignment. |

### Localization and binding

| Area | UWP behavior | Portable/Avalonia behavior |
|---|---|---|
| `.resw` lookup | `Windows.ApplicationModel.Resources.ResourceLoader` resolves package maps and culture fallback. | The port provides the same namespace and compatible lookup surface over shipped `.resw` files. |
| `x:Uid` properties | UWP projects resource properties such as `Foo.Text` and attached-property names into XAML automatically. | Avalonia does not understand UWP `x:Uid`. Read keys explicitly or use `GetUidProperties` in an adapter. |
| Attached-property keys | Resource names can contain forms such as `Uid.[using:…]AutomationProperties.Name`. | Preserve the full suffix. The compatibility loader converts the last `Uid/Property` separator to the `.resw` dot form. |
| Compiled binding | UWP `x:Bind` has source-specific generated semantics. | Avalonia compiled bindings require `x:DataType`. For a data template that must reach an ancestor window command, either provide a typed binding path or intentionally disable compiled binding for that small template. |
| Command-generated state | C++/CX view models expose WinRT commands and observable properties. | CommunityToolkit.Mvvm generates commands and property notifications in the managed compatibility layer. Computed visibility/selection properties must declare dependent notifications explicitly. |

## Reusable Avalonia patterns

### Own fidelity-critical button templates

Setting `Background="Transparent"` is not sufficient when the Fluent template
contains independent pointer-over, pressed, or disabled layers. For controls
whose WinUI appearance matters, use a minimal template and then add only the
states present in the source application.

```xml
<Style Selector="Button.transparentInteractionButton">
  <Setter Property="Template">
    <ControlTemplate>
      <Border Background="Transparent">
        <ContentPresenter Content="{TemplateBinding Content}"
                          HorizontalContentAlignment="Center"
                          VerticalContentAlignment="Center" />
      </Border>
    </ControlTemplate>
  </Setter>
</Style>
```

### Keep motion state separate from destination state

`IsNavigationPaneOpen` describes the destination. A second
`IsNavigationPaneTransitioning` state prevents reentry and blocks hit testing
until the visual transition has finished. This avoids double toggles and keeps
the persistent hamburger above the moving pane without disabled-state dimming.

### Use dynamic resources for user-selectable themes

Stock controls react to `RequestedThemeVariant`; custom Calculator surfaces do
not unless their values come from theme dictionaries through `DynamicResource`.
Any literal dark color in a control template should be treated as a Light-mode
bug until proven otherwise.

## Visual QA checklist for each migrated surface

1. Compare normal, pointer-over, pressed, disabled, focused, and selected states.
2. Test minimum size, reference size, wide size, and live resizing.
3. Test Dark, Light, and system theme while the surface is already open.
4. Check keyboard focus and activation separately from pointer visuals.
5. Inspect icon font loading in both `dotnet run` and packaged app builds.
6. Verify text and geometry with a long-string locale and an RTL locale.
7. Verify window drag and resize hit targets after adding any full-window overlay.
8. Check that invisible hit targets have owned transparent templates and cannot flash.
9. Verify animation layering frame-by-frame: stationary controls must remain above translating surfaces.
10. Launch the packaged macOS app and confirm bundle, Dock, process, and window labels independently.

## Known gaps still requiring comparison

- Scientific and Programmer toggle/radio templates
- DatePicker and CalendarView parity for Date Calculation
- History and Memory flyouts
- Compact Overlay / Always-on-top mode
- Keyboard accelerators, access keys, focus visuals, and narrator announcements
- High Contrast behavior
- RTL pane motion and content mirroring
- Currency network/error/loading states
- Graphing controls and unavailable proprietary graphing-engine states

Add each newly observed discrepancy to the behavior matrix before applying an
override. This keeps source behavior, framework defaults, and port-specific
decisions distinguishable during the remainder of the migration.
