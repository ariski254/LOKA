using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using Loka.App.ViewModels;
using Loka.App.Views;
using Loka.Core.Interfaces;
using Loka.Infrastructure.Configuration;
using Loka.Infrastructure.Logging;
using Loka.Infrastructure.Networking;
using Loka.Runtime.Detector;
using Loka.Runtime.Php;
using Xunit;
using Xunit.Abstractions;

namespace Loka.Tests;

public class ButtonAuditTests
{
    private readonly ITestOutputHelper _output;

    public ButtonAuditTests(ITestOutputHelper output)
    {
        _output = output;
    }
    private void RunInSta(Action action)
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
        {
            throw new AggregateException("STA thread failed", threadEx);
        }
    }

    private static string GetHexColor(Brush? brush)
    {
        if (brush is SolidColorBrush scb)
        {
            return $"#{scb.Color.R:X2}{scb.Color.G:X2}{scb.Color.B:X2}";
        }
        return "Unknown";
    }

    private ResourceDictionary LoadAppResources()
    {
        var appRes = new ResourceDictionary();
        appRes["BoolToVis"] = new Loka.App.Converters.UniversalBooleanToVisibilityConverter();
        appRes["InverseBoolToVis"] = new Loka.App.Converters.InverseBooleanToVisibilityConverter();
        appRes["InverseBool"] = new Loka.App.Converters.InverseBooleanConverter();
        var colors = new ResourceDictionary { Source = new Uri("pack://application:,,,/Loka;component/Themes/Colors.xaml", UriKind.RelativeOrAbsolute) };
        var styles = new ResourceDictionary { Source = new Uri("pack://application:,,,/Loka;component/Themes/Styles.xaml", UriKind.RelativeOrAbsolute) };
        appRes.MergedDictionaries.Add(colors);
        appRes.MergedDictionaries.Add(styles);
        return appRes;
    }

    private ResourceDictionary LoadInstallerResources()
    {
        var installerApp = new ResourceDictionary { Source = new Uri("pack://application:,,,/Loka-Setup;component/App.xaml", UriKind.RelativeOrAbsolute) };
        return installerApp;
    }

    [Fact]
    public void VerifyButtonStylesAndHierarchy()
    {
        RunInSta(() =>
        {
            if (Application.Current == null)
            {
                new Application();
            }

            var res = LoadAppResources();

            // 1. Primary Button Style
            var primaryStyle = res["PrimaryButton"] as Style;
            Assert.NotNull(primaryStyle);
            var primaryBg = primaryStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.BackgroundProperty)?.Value as SolidColorBrush;
            var primaryFg = primaryStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.ForegroundProperty)?.Value as SolidColorBrush;
            Assert.Equal("#2563EB", GetHexColor(primaryBg));
            Assert.Equal("#FFFFFF", GetHexColor(primaryFg));

            // 2. Secondary Button Style
            var secondaryStyle = res["SecondaryButton"] as Style;
            Assert.NotNull(secondaryStyle);
            var secBg = secondaryStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.BackgroundProperty)?.Value as SolidColorBrush;
            var secFg = secondaryStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.ForegroundProperty)?.Value as SolidColorBrush;
            var secBorder = secondaryStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.BorderBrushProperty)?.Value as SolidColorBrush;
            Assert.Equal("#FFFFFF", GetHexColor(secBg));
            Assert.Equal("#18181B", GetHexColor(secFg));
            Assert.Equal("#E4E4E7", GetHexColor(secBorder));

            // 3. Danger / Destructive Button Style
            var dangerStyle = res["DangerButton"] as Style;
            Assert.NotNull(dangerStyle);
            var dangerBg = dangerStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.BackgroundProperty)?.Value as SolidColorBrush;
            var dangerFg = dangerStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.ForegroundProperty)?.Value as SolidColorBrush;
            var dangerBorder = dangerStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.BorderBrushProperty)?.Value as SolidColorBrush;
            Assert.Equal("#FFFFFF", GetHexColor(dangerBg));
            Assert.Equal("#DC2626", GetHexColor(dangerFg));
            Assert.Equal("#E4E4E7", GetHexColor(dangerBorder));

            // Test runtime template visual elements
            var btnPrimary = new Button { Style = primaryStyle, Content = "Set Up Environment →" };
            Assert.Equal("#2563EB", GetHexColor(btnPrimary.Background));
            Assert.Equal("#FFFFFF", GetHexColor(btnPrimary.Foreground));

            var btnSecondary = new Button { Style = secondaryStyle, Content = "Refresh" };
            Assert.Equal("#FFFFFF", GetHexColor(btnSecondary.Background));
            Assert.Equal("#18181B", GetHexColor(btnSecondary.Foreground));

            var btnDestructive = new Button { Style = dangerStyle, Content = "Uninstall" };
            Assert.Equal("#FFFFFF", GetHexColor(btnDestructive.Background));
            Assert.Equal("#DC2626", GetHexColor(btnDestructive.Foreground));

            // Verify actual TextBlock inside Visual Tree has correct Foreground
            var win = new Window { Width = 300, Height = 300 };
            var sp = new StackPanel();
            sp.Children.Add(btnPrimary);
            sp.Children.Add(btnSecondary);
            sp.Children.Add(btnDestructive);
            win.Content = sp;
            win.Show();
            btnPrimary.UpdateLayout();
            btnSecondary.UpdateLayout();
            btnDestructive.UpdateLayout();

            var primaryTb = FindVisualChildren<TextBlock>(btnPrimary).FirstOrDefault();
            Assert.NotNull(primaryTb);
            Assert.Equal("#FFFFFF", GetHexColor(primaryTb.Foreground));
            _output.WriteLine($"[TEST RUNTIME] PrimaryButton: Background={GetHexColor(btnPrimary.Background)}, TextBlock Foreground={GetHexColor(primaryTb.Foreground)}");

            var secTb = FindVisualChildren<TextBlock>(btnSecondary).FirstOrDefault();
            Assert.NotNull(secTb);
            Assert.Equal("#18181B", GetHexColor(secTb.Foreground));
            _output.WriteLine($"[TEST RUNTIME] SecondaryButton: Background={GetHexColor(btnSecondary.Background)}, TextBlock Foreground={GetHexColor(secTb.Foreground)}");

            var dangerTb = FindVisualChildren<TextBlock>(btnDestructive).FirstOrDefault();
            Assert.NotNull(dangerTb);
            Assert.Equal("#DC2626", GetHexColor(dangerTb.Foreground));
            _output.WriteLine($"[TEST RUNTIME] DangerButton: Background={GetHexColor(btnDestructive.Background)}, TextBlock Foreground={GetHexColor(dangerTb.Foreground)}");

            win.Close();

            // 4. Primary Template Triggers Verification (Hover, Pressed, Disabled)
            var primaryTemplate = primaryStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.TemplateProperty)?.Value as ControlTemplate;
            Assert.NotNull(primaryTemplate);
            var pMouseOver = primaryTemplate.Triggers.OfType<Trigger>().FirstOrDefault(t => t.Property == UIElement.IsMouseOverProperty && (bool)t.Value);
            Assert.NotNull(pMouseOver);
            var pMouseBg = pMouseOver.Setters.OfType<Setter>().FirstOrDefault(s => s.TargetName == "BtnBorder" && s.Property == Border.BackgroundProperty)?.Value as SolidColorBrush;
            Assert.Equal("#1D4ED8", GetHexColor(pMouseBg));

            var pPressed = primaryTemplate.Triggers.OfType<Trigger>().FirstOrDefault(t => t.Property == ButtonBase.IsPressedProperty && (bool)t.Value);
            Assert.NotNull(pPressed);
            var pPressedBg = pPressed.Setters.OfType<Setter>().FirstOrDefault(s => s.TargetName == "BtnBorder" && s.Property == Border.BackgroundProperty)?.Value as SolidColorBrush;
            Assert.Equal("#1E40AF", GetHexColor(pPressedBg));

            var pDisabled = primaryTemplate.Triggers.OfType<Trigger>().FirstOrDefault(t => t.Property == UIElement.IsEnabledProperty && !(bool)t.Value);
            Assert.NotNull(pDisabled);
            var pDisabledBg = pDisabled.Setters.OfType<Setter>().FirstOrDefault(s => s.TargetName == "BtnBorder" && s.Property == Border.BackgroundProperty)?.Value as SolidColorBrush;
            var pDisabledFg = pDisabled.Setters.OfType<Setter>().FirstOrDefault(s => s.TargetName == "BtnBorder" && s.Property == TextElement.ForegroundProperty)?.Value as SolidColorBrush;
            Assert.Equal("#F4F4F5", GetHexColor(pDisabledBg));
            Assert.Equal("#A1A1AA", GetHexColor(pDisabledFg));

            // 5. Secondary Template Triggers Verification (Hover, Disabled)
            var secTemplate = secondaryStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.TemplateProperty)?.Value as ControlTemplate;
            Assert.NotNull(secTemplate);
            var sMouseOver = secTemplate.Triggers.OfType<Trigger>().FirstOrDefault(t => t.Property == UIElement.IsMouseOverProperty && (bool)t.Value);
            Assert.NotNull(sMouseOver);
            var sMouseBg = sMouseOver.Setters.OfType<Setter>().FirstOrDefault(s => s.TargetName == "BtnBorder" && s.Property == Border.BackgroundProperty)?.Value as SolidColorBrush;
            Assert.Equal("#F4F4F5", GetHexColor(sMouseBg));

            var sDisabled = secTemplate.Triggers.OfType<Trigger>().FirstOrDefault(t => t.Property == UIElement.IsEnabledProperty && !(bool)t.Value);
            Assert.NotNull(sDisabled);
            var sDisabledBg = sDisabled.Setters.OfType<Setter>().FirstOrDefault(s => s.TargetName == "BtnBorder" && s.Property == Border.BackgroundProperty)?.Value as SolidColorBrush;
            var sDisabledFg = sDisabled.Setters.OfType<Setter>().FirstOrDefault(s => s.TargetName == "BtnBorder" && s.Property == TextElement.ForegroundProperty)?.Value as SolidColorBrush;
            Assert.Equal("#F4F4F5", GetHexColor(sDisabledBg));
            Assert.Equal("#A1A1AA", GetHexColor(sDisabledFg));

            // 6. Destructive Template Triggers Verification (Hover, Disabled)
            var dangerTemplate = dangerStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.TemplateProperty)?.Value as ControlTemplate;
            Assert.NotNull(dangerTemplate);
            var dMouseOver = dangerTemplate.Triggers.OfType<Trigger>().FirstOrDefault(t => t.Property == UIElement.IsMouseOverProperty && (bool)t.Value);
            Assert.NotNull(dMouseOver);
            var dMouseBg = dMouseOver.Setters.OfType<Setter>().FirstOrDefault(s => s.TargetName == "BtnBorder" && s.Property == Border.BackgroundProperty)?.Value as SolidColorBrush;
            Assert.Equal("#FEF2F2", GetHexColor(dMouseBg));

            var dDisabled = dangerTemplate.Triggers.OfType<Trigger>().FirstOrDefault(t => t.Property == UIElement.IsEnabledProperty && !(bool)t.Value);
            Assert.NotNull(dDisabled);
            var dDisabledBg = dDisabled.Setters.OfType<Setter>().FirstOrDefault(s => s.TargetName == "BtnBorder" && s.Property == Border.BackgroundProperty)?.Value as SolidColorBrush;
            var dDisabledFg = dDisabled.Setters.OfType<Setter>().FirstOrDefault(s => s.TargetName == "BtnBorder" && s.Property == TextElement.ForegroundProperty)?.Value as SolidColorBrush;
            Assert.Equal("#F4F4F5", GetHexColor(dDisabledBg));
            Assert.Equal("#A1A1AA", GetHexColor(dDisabledFg));
        });
    }

    [Fact]
    public void VerifyTarget15ButtonsAcrossViews()
    {
        RunInSta(() =>
        {
            if (Application.Current == null)
            {
                new Application();
            }

            var appRes = LoadAppResources();
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(appRes);

            var logger = new LogService();
            var configService = new ConfigurationService(logger);
            var portService = new PortService(logger);
            var processManager = new Loka.Infrastructure.Process.ProcessManager(logger);
            var runtimeDetector = new RuntimeDetector(configService, logger);
            var phpService = new PhpService(configService, runtimeDetector, processManager, logger);

            // Audit Target Buttons in SetupWizardWindow
            var setupVm = new SetupWizardViewModel(configService, runtimeDetector, portService, phpService, logger);
            var setupWindow = new SetupWizardWindow(setupVm);

            // Find all buttons in setupWindow
            var buttons = FindVisualChildren<Button>(setupWindow).ToList();
            Assert.NotEmpty(buttons);

            // Check specific target buttons in SetupWizard:
            // 1. "Set Up Environment →"
            var btnSetupEnv = buttons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Set Up Environment") == true);
            Assert.NotNull(btnSetupEnv);
            Assert.Equal("#2563EB", GetHexColor(btnSetupEnv.Background));
            Assert.Equal("#FFFFFF", GetHexColor(btnSetupEnv.Foreground));
            _output.WriteLine($"Target 1: Setup Environment -> Background={GetHexColor(btnSetupEnv.Background)}, Foreground={GetHexColor(btnSetupEnv.Foreground)} [PASSED]");

            // 2. "Continue →"
            var btnContinue = buttons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Continue") == true);
            Assert.NotNull(btnContinue);
            Assert.Equal("#2563EB", GetHexColor(btnContinue.Background));
            Assert.Equal("#FFFFFF", GetHexColor(btnContinue.Foreground));
            _output.WriteLine($"Target 2: Continue -> Background={GetHexColor(btnContinue.Background)}, Foreground={GetHexColor(btnContinue.Foreground)} [PASSED]");

            // 3. "Apply Setup"
            var btnApplySetup = buttons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Apply Setup") == true);
            Assert.NotNull(btnApplySetup);
            Assert.Equal("#2563EB", GetHexColor(btnApplySetup.Background));
            Assert.Equal("#FFFFFF", GetHexColor(btnApplySetup.Foreground));
            _output.WriteLine($"Target 3: Apply Setup -> Background={GetHexColor(btnApplySetup.Background)}, Foreground={GetHexColor(btnApplySetup.Foreground)} [PASSED]");

            // 4. "Open Loka Dashboard →"
            var btnOpenDash = buttons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Open Loka Dashboard") == true);
            Assert.NotNull(btnOpenDash);
            Assert.Equal("#2563EB", GetHexColor(btnOpenDash.Background));
            Assert.Equal("#FFFFFF", GetHexColor(btnOpenDash.Foreground));
            _output.WriteLine($"Target 4: Open Loka Dashboard -> Background={GetHexColor(btnOpenDash.Background)}, Foreground={GetHexColor(btnOpenDash.Foreground)} [PASSED]");

            // 14. "← Back"
            var btnBack = buttons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Back") == true);
            Assert.NotNull(btnBack);
            Assert.Equal("#FFFFFF", GetHexColor(btnBack.Background));
            Assert.Equal("#18181B", GetHexColor(btnBack.Foreground));
            _output.WriteLine($"Target 14: Back -> Background={GetHexColor(btnBack.Background)}, Foreground={GetHexColor(btnBack.Foreground)} [PASSED]");

            // 15. "Cancel" in setup wizard
            var btnCancel = buttons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Cancel") == true);
            Assert.NotNull(btnCancel);
            Assert.Equal("#FFFFFF", GetHexColor(btnCancel.Background));
            Assert.Equal("#18181B", GetHexColor(btnCancel.Foreground));
            _output.WriteLine($"Target 15: Cancel -> Background={GetHexColor(btnCancel.Background)}, Foreground={GetHexColor(btnCancel.Foreground)} [PASSED]");

            // ProjectsView Audit
            var projView = new ProjectsView();
            var projButtons = FindVisualChildren<Button>(projView).ToList();
            var btnNew = projButtons.FirstOrDefault(b => b.Content?.ToString()?.Contains("New") == true);
            Assert.NotNull(btnNew);
            // Toolbar "+ New" is Secondary
            Assert.Equal("#FFFFFF", GetHexColor(btnNew.Background));
            Assert.Equal("#18181B", GetHexColor(btnNew.Foreground));
            _output.WriteLine($"Target 5: New -> Background={GetHexColor(btnNew.Background)}, Foreground={GetHexColor(btnNew.Foreground)} [PASSED]");

            // RuntimesView Audit
            var runtimesView = new RuntimesView();
            var runtimesButtons = FindVisualChildren<Button>(runtimesView).ToList();
            var btnRefreshRuntimes = runtimesButtons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Refresh") == true);
            Assert.NotNull(btnRefreshRuntimes);
            Assert.Equal("#FFFFFF", GetHexColor(btnRefreshRuntimes.Background));
            Assert.Equal("#18181B", GetHexColor(btnRefreshRuntimes.Foreground));
            _output.WriteLine($"Target 11: Refresh -> Background={GetHexColor(btnRefreshRuntimes.Background)}, Foreground={GetHexColor(btnRefreshRuntimes.Foreground)} [PASSED]");

            // DatabaseView Audit
            var dbView = new DatabaseView();
            var dbButtons = FindVisualChildren<Button>(dbView).ToList();
            var btnCreateDb = dbButtons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Create") == true);
            Assert.NotNull(btnCreateDb);
            Assert.Equal("#2563EB", GetHexColor(btnCreateDb.Background));
            Assert.Equal("#FFFFFF", GetHexColor(btnCreateDb.Foreground));
            _output.WriteLine($"Target 7: Create -> Background={GetHexColor(btnCreateDb.Background)}, Foreground={GetHexColor(btnCreateDb.Foreground)} [PASSED]");

            var btnRunQuery = dbButtons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Run Query") == true);
            Assert.NotNull(btnRunQuery);
            Assert.Equal("#2563EB", GetHexColor(btnRunQuery.Background));
            Assert.Equal("#FFFFFF", GetHexColor(btnRunQuery.Foreground));
            _output.WriteLine($"Target 8: Run Query -> Background={GetHexColor(btnRunQuery.Background)}, Foreground={GetHexColor(btnRunQuery.Foreground)} [PASSED]");

            // DatabaseView Delete Button Audit (Destructive / Danger)
            var btnDeleteDb = dbButtons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Delete") == true);
            Assert.NotNull(btnDeleteDb);
            Assert.Equal("#FFFFFF", GetHexColor(btnDeleteDb.Background));
            Assert.Equal("#DC2626", GetHexColor(btnDeleteDb.Foreground));
            _output.WriteLine($"Target 10: Delete (Uninstall/Remove DB) -> Background={GetHexColor(btnDeleteDb.Background)}, Foreground={GetHexColor(btnDeleteDb.Foreground)} [PASSED]");

            var btnTestConn = dbButtons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Test Connection") == true);
            Assert.NotNull(btnTestConn);
            Assert.Equal("#FFFFFF", GetHexColor(btnTestConn.Background));
            Assert.Equal("#18181B", GetHexColor(btnTestConn.Foreground));

            // ProjectsView Additional Audit
            var btnSync = projButtons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Sync") == true);
            Assert.NotNull(btnSync);
            Assert.Equal("#FFFFFF", GetHexColor(btnSync.Background));
            Assert.Equal("#18181B", GetHexColor(btnSync.Foreground));

            // RuntimesView Open php.ini Audit
            var btnPhpIni = runtimesButtons.FirstOrDefault(b => b.Content?.ToString()?.Contains("php.ini") == true);
            Assert.NotNull(btnPhpIni);
            Assert.Equal("#FFFFFF", GetHexColor(btnPhpIni.Background));
            Assert.Equal("#18181B", GetHexColor(btnPhpIni.Foreground));

            // SettingsView Audit
            var settingsView = new SettingsView();
            var settingsButtons = FindVisualChildren<Button>(settingsView).ToList();
            var btnSaveSettings = settingsButtons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Save Changes") == true);
            Assert.NotNull(btnSaveSettings);
            Assert.Equal("#2563EB", GetHexColor(btnSaveSettings.Background));
            Assert.Equal("#FFFFFF", GetHexColor(btnSaveSettings.Foreground));
            _output.WriteLine($"Target 9: Save Changes -> Background={GetHexColor(btnSaveSettings.Background)}, Foreground={GetHexColor(btnSaveSettings.Foreground)} [PASSED]");
        });
    }

    [Fact]
    public void VerifyInstallerAndUninstallerButtons()
    {
        RunInSta(() =>
        {
            if (Application.Current == null)
            {
                new Application();
            }

            var appXamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Loka.Installer/App.xaml"));
            var xamlContent = File.ReadAllText(appXamlPath);
            int start = xamlContent.IndexOf("<ResourceDictionary");
            int end = xamlContent.LastIndexOf("</ResourceDictionary>") + "</ResourceDictionary>".Length;
            var dictXaml = xamlContent.Substring(start, end - start);
            dictXaml = dictXaml.Replace("<ResourceDictionary", "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"");
            var installerRes = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(dictXaml);

            var dangerStyle = installerRes["DangerButton"] as Style;
            Assert.NotNull(dangerStyle);
            var dangerBg = dangerStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.BackgroundProperty)?.Value as SolidColorBrush;
            var dangerFg = dangerStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.ForegroundProperty)?.Value as SolidColorBrush;
            Assert.Equal("#FFFFFF", GetHexColor(dangerBg));
            Assert.Equal("#DC2626", GetHexColor(dangerFg));

            var primaryStyle = installerRes["PrimaryButton"] as Style;
            Assert.NotNull(primaryStyle);
            var primaryBg = primaryStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.BackgroundProperty)?.Value as SolidColorBrush;
            var primaryFg = primaryStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.ForegroundProperty)?.Value as SolidColorBrush;
            Assert.Equal("#2563EB", GetHexColor(primaryBg));
            Assert.Equal("#FFFFFF", GetHexColor(primaryFg));

            var secStyle = installerRes["SecondaryButton"] as Style;
            Assert.NotNull(secStyle);
            var secBg = secStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.BackgroundProperty)?.Value as SolidColorBrush;
            var secFg = secStyle.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == Control.ForegroundProperty)?.Value as SolidColorBrush;
            Assert.Equal("#FFFFFF", GetHexColor(secBg));
            Assert.Equal("#18181B", GetHexColor(secFg));
        });
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T t)
            {
                yield return t;
            }

            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }

        // Also inspect ContentControl or Panels that may not yet be in visual tree until rendered
        if (depObj is ContentControl cc && cc.Content is DependencyObject childDo)
        {
            if (childDo is T tChild) yield return tChild;
            foreach (var c in FindVisualChildren<T>(childDo)) yield return c;
        }
        else if (depObj is Panel panel)
        {
            foreach (UIElement element in panel.Children)
            {
                if (element is T tEl) yield return tEl;
                foreach (var c in FindVisualChildren<T>(element)) yield return c;
            }
        }
        else if (depObj is Decorator decorator && decorator.Child != null)
        {
            if (decorator.Child is T tDec) yield return tDec;
            foreach (var c in FindVisualChildren<T>(decorator.Child)) yield return c;
        }
    }
}
