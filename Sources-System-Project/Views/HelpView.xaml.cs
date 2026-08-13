using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Sources.ViewModels;
using Sources.Models;

namespace Sources.Views;

public partial class HelpView : UserControl
{
    public HelpView()
    {
        InitializeComponent();
    }

    private void TopicsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TopicsList.SelectedItem is HelpTopic topic)
        {
            RenderContent(topic);
        }
    }

    private void RenderContent(HelpTopic topic)
    {
        RichContentArea.Children.Clear();

        // Update header icon and title
        ContentIcon.Kind = (MaterialDesignThemes.Wpf.PackIconKind)Enum.Parse(typeof(MaterialDesignThemes.Wpf.PackIconKind), topic.IconKind);
        ContentTitle.Text = topic.DisplayTitle;

        // Generate rich content based on topic
        switch (topic.TitleKey)
        {
            case "HelpTopicIntroTitle": RenderIntro(); break;
            case "HelpTopicDashboardTitle": RenderDashboard(); break;
            case "HelpTopicRadioisotopesTitle": RenderRadioisotopes(); break;
            case "HelpTopicSourcesTitle": RenderSources(); break;
            case "HelpTopicBorrowingTitle": RenderBorrowing(); break;
            case "HelpTopicReportsTitle": RenderReports(); break;
            case "HelpTopicSettingsTitle": RenderSettings(); break;
        }
    }

    // ─── Builders ───

    private TextBlock MakeParagraph(string text, double fontSize = 15, bool bold = false)
    {
        var tb = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = fontSize,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = (Brush)FindResource("TextPrimary"),
            Margin = new Thickness(0, 0, 0, 12),
            LineHeight = 26
        };
        return tb;
    }

    private Border MakeSectionHeader(string iconKind, string text)
    {
        var icon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = (MaterialDesignThemes.Wpf.PackIconKind)Enum.Parse(typeof(MaterialDesignThemes.Wpf.PackIconKind), iconKind),
            Width = 20, Height = 20,
            Foreground = new SolidColorBrush(Color.FromRgb(212, 175, 55)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        var label = new TextBlock
        {
            Text = text,
            FontSize = 18, FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimary"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(icon);
        sp.Children.Add(label);
        var border = new Border
        {
            Margin = new Thickness(0, 18, 0, 12),
            Padding = new Thickness(0, 0, 0, 8),
            BorderBrush = new SolidColorBrush(Color.FromArgb(50, 212, 175, 55)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = sp
        };
        return border;
    }

    private Border MakeStepBlock(string stepNum, string title, string description)
    {
        var numBorder = new Border
        {
            Width = 32, Height = 32, CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromArgb(30, 212, 175, 55)),
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = stepNum,
                FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(212, 175, 55)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var textPanel = new StackPanel();
        textPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextPrimary"),
            Margin = new Thickness(0, 0, 0, 4)
        });
        textPanel.Children.Add(new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = (Brush)FindResource("TextSecondary"),
            LineHeight = 22
        });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(numBorder, 0);
        Grid.SetColumn(textPanel, 1);
        grid.Children.Add(numBorder);
        grid.Children.Add(textPanel);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(8, 212, 175, 55)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 8),
            Child = grid
        };
    }

    private Border MakeTipBlock(string text)
    {
        var icon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.LightbulbOnOutline,
            Width = 20, Height = 20,
            Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 12, 0)
        };
        var label = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = (Brush)FindResource("TextPrimary"),
            LineHeight = 22,
            VerticalAlignment = VerticalAlignment.Center
        };
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(icon);
        sp.Children.Add(label);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(15, 76, 175, 80)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 76, 175, 80)),
            BorderThickness = new Thickness(0, 0, 3, 0),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 6, 0, 12),
            Child = sp
        };
    }

    private Border MakeWarningBlock(string text)
    {
        var icon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertCircleOutline,
            Width = 20, Height = 20,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 12, 0)
        };
        var label = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = (Brush)FindResource("TextPrimary"),
            LineHeight = 22
        };
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(icon);
        sp.Children.Add(label);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(15, 255, 152, 0)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 152, 0)),
            BorderThickness = new Thickness(0, 0, 3, 0),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 6, 0, 12),
            Child = sp
        };
    }

    private Border MakeBulletList(params string[] items)
    {
        var sp = new StackPanel { Margin = new Thickness(8, 0, 0, 12) };
        foreach (var item in items)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            row.Children.Add(new TextBlock
            {
                Text = "●",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(212, 175, 55)),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 5, 10, 0)
            });
            row.Children.Add(new TextBlock
            {
                Text = item,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = (Brush)FindResource("TextPrimary"),
                LineHeight = 22,
                MaxWidth = 550
            });
            sp.Children.Add(row);
        }
        return new Border { Child = sp };
    }

    private Border MakeExampleBlock(string title, string content)
    {
        var headerIcon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.CodeBraces,
            Width = 16, Height = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var headerText = new TextBlock
        {
            Text = title,
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            VerticalAlignment = VerticalAlignment.Center
        };
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        headerRow.Children.Add(headerIcon);
        headerRow.Children.Add(headerText);

        var bodyText = new TextBlock
        {
            Text = content,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = (Brush)FindResource("TextPrimary"),
            LineHeight = 22,
            FontFamily = new FontFamily("Consolas, Courier New, monospace")
        };

        var sp = new StackPanel();
        sp.Children.Add(headerRow);
        sp.Children.Add(bodyText);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(12, 33, 150, 243)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(50, 33, 150, 243)),
            BorderThickness = new Thickness(0, 0, 3, 0),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 6, 0, 14),
            Child = sp
        };
    }

    // ─── Content Renderers ───

    private bool IsArabic()
    {
        return FlowDirection == FlowDirection.RightToLeft ||
               System.Threading.Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName == "ar";
    }

    private void RenderIntro()
    {
        var ar = IsArabic();
        var p = RichContentArea;

        p.Children.Add(MakeParagraph(
            ar ? "مرحباً بك في منظومة مسار — نظامك المتكامل لإدارة وتتبع المصادر المشعة بكفاءة عالية وأمان مطلق."
               : "Welcome to the Sources System — your integrated platform for managing and tracking radioactive sources with high efficiency and complete safety.",
            16));

        p.Children.Add(MakeSectionHeader("Navigation", ar ? "كيف تبدأ؟" : "Getting Started"));

        p.Children.Add(MakeStepBlock("1",
            ar ? "تسجيل الدخول" : "Login",
            ar ? "أدخل اسم المستخدم وكلمة المرور المسجلين لدى مدير النظام. يمكنك تفعيل \"تذكرني\" لتسهيل الدخول لاحقاً."
               : "Enter the username and password registered by the system administrator. You can enable 'Remember Me' for easier future logins."));

        p.Children.Add(MakeStepBlock("2",
            ar ? "استكشاف لوحة القيادة" : "Explore the Dashboard",
            ar ? "بعد الدخول، ستظهر لوحة القيادة التي تعرض ملخصاً شاملاً لحالة جميع المصادر في المنشأة."
               : "After login, the Dashboard appears showing a comprehensive summary of all sources in the facility."));

        p.Children.Add(MakeStepBlock("3",
            ar ? "استخدام القائمة الجانبية" : "Use the Sidebar",
            ar ? "القائمة الجانبية هي بوابتك لكل أقسام النظام. اضغط على أي قسم للانتقال إليه مباشرة."
               : "The sidebar is your gateway to all system sections. Click any section to navigate directly."));

        p.Children.Add(MakeTipBlock(
            ar ? "💡 نصيحة: يمكنك طي القائمة الجانبية بالضغط على أيقونة ☰ في الشريط العلوي لتوسيع مساحة العرض."
               : "💡 Tip: You can collapse the sidebar by clicking the ☰ icon in the top bar to expand your viewing area."));

        p.Children.Add(MakeSectionHeader("Tune", ar ? "واجهة المستخدم" : "User Interface"));

        p.Children.Add(MakeBulletList(
            ar ? "القائمة الجانبية: تحتوي على جميع أقسام النظام الرئيسية" : "Sidebar: Contains all main system sections",
            ar ? "الشريط العلوي: يحتوي على عنوان الصفحة الحالية + جرس التنبيهات" : "Top Bar: Contains the current page title + notification bell",
            ar ? "منطقة المحتوى: المساحة الرئيسية التي تعرض واجهة القسم المختار" : "Content Area: The main space displaying the selected section",
            ar ? "تبديل المظهر: أيقونة الشمس/القمر لتبديل الوضع الداكن والفاتح" : "Theme Toggle: Sun/Moon icon to switch between Dark and Light mode"));

        p.Children.Add(MakeExampleBlock(
            ar ? "مثال: التنقل بين الأقسام" : "Example: Navigating between sections",
            ar ? "اضغط على \"المصادر المشعة\" ← تظهر قائمة الجرد\nاضغط على \"التقارير\" ← تظهر شاشة التقارير\nاضغط على \"الاستعارة\" ← تظهر شاشة إدارة طلبات الاستعارة"
               : "Click 'Radioactive Sources' → Inventory list appears\nClick 'Reports' → Reports screen appears\nClick 'Borrowing' → Borrowing management screen appears"));
    }

    private void RenderDashboard()
    {
        var ar = IsArabic();
        var p = RichContentArea;

        p.Children.Add(MakeParagraph(
            ar ? "لوحة القيادة هي الشاشة الرئيسية التي تمنحك صورة شاملة وفورية عن حالة المنظومة بأكملها."
               : "The Dashboard is the main screen that gives you a comprehensive, real-time view of the entire system status.",
            16));

        p.Children.Add(MakeSectionHeader("ChartBox", ar ? "بطاقات المؤشرات" : "KPI Cards"));

        p.Children.Add(MakeBulletList(
            ar ? "عدد المصادر المسجلة: إجمالي جميع المصادر في قاعدة البيانات" : "Registered Sources: Total of all sources in the database",
            ar ? "التوزيع: النشط مقابل المخزن مقابل يحتاج إجراء" : "Distribution: Active vs. Stored vs. Requires Action",
            ar ? "إجمالي النشاط الإشعاعي: مجموع النشاط الحالي لجميع المصادر" : "Total Radioactive Activity: Sum of current activity for all sources"));

        p.Children.Add(MakeExampleBlock(
            ar ? "مثال: قراءة بطاقة المؤشرات" : "Example: Reading the KPI Card",
            ar ? "إذا ظهرت البطاقة:\n  المصادر المسجلة: 45\n  النشطة: 38  |  المخزنة: 5  |  تحتاج إجراء: 2\nفهذا يعني أن لديك مصدرين يحتاجان لمراجعة فورية (معايرة أو تخلص)."
               : "If the card shows:\n  Registered Sources: 45\n  Active: 38  |  Stored: 5  |  Requires Action: 2\nThis means 2 sources need your immediate review (calibration or disposal)."));

        p.Children.Add(MakeSectionHeader("ChartLine", ar ? "الرسوم البيانية" : "Charts"));

        p.Children.Add(MakeParagraph(
            ar ? "تعرض الرسوم البيانية التفاعلية توزيع النشاط الإشعاعي بمرور الوقت وتوزيع النظائر المشعة المستخدمة."
               : "Interactive charts display the distribution of radioactive activity over time and the radioisotope usage distribution.",
            15));

        p.Children.Add(MakeTipBlock(
            ar ? "💡 نصيحة: راقب مؤشر \"يحتاج إجراء\" يومياً — إذا زاد العدد فتحقق من تنبيهات المعايرة والمصادر منخفضة النشاط."
               : "💡 Tip: Monitor the 'Requires Action' indicator daily — if the count increases, check calibration alerts and low activity sources."));

        p.Children.Add(MakeSectionHeader("Table", ar ? "جدول أحدث الاستعارات" : "Recent Borrowings Table"));

        p.Children.Add(MakeParagraph(
            ar ? "في الجزء السفلي من لوحة القيادة، يظهر جدول بأحدث طلبات الاستعارة مع حالة كل طلب (معلق، مسلّم، مرتجع)."
               : "At the bottom of the Dashboard, a table shows the latest borrowing requests with the status of each request (Pending, Delivered, Returned).",
            15));
    }

    private void RenderRadioisotopes()
    {
        var ar = IsArabic();
        var p = RichContentArea;

        p.Children.Add(MakeParagraph(
            ar ? "قسم النظائر المشعة يتيح لك تسجيل وإدارة جميع أنواع النظائر المستخدمة في المنشأة. هذا القسم هو الأساس الذي تبنى عليه بيانات المصادر."
               : "The Radioisotopes section lets you register and manage all types of isotopes used in your facility. This section is the foundation for source data.",
            16));

        p.Children.Add(MakeSectionHeader("Plus", ar ? "إضافة نظير جديد" : "Adding a New Isotope"));

        p.Children.Add(MakeStepBlock("1",
            ar ? "فتح نموذج الإضافة" : "Open the Add Form",
            ar ? "اضغط على زر \"➕ إضافة نظير جديد\" في أعلى الصفحة." : "Click '➕ Add New Isotope' at the top of the page."));

        p.Children.Add(MakeStepBlock("2",
            ar ? "إدخال هوية النظير" : "Enter Isotope Identity",
            ar ? "أدخل الرمز الكيميائي (مثال: Cs-137)، الاسم بالعربي والإنجليزي." : "Enter the chemical symbol (e.g., Cs-137), name in Arabic and English."));

        p.Children.Add(MakeStepBlock("3",
            ar ? "الخصائص الفنية" : "Technical Properties",
            ar ? "أدخل نصف العمر (مثال: 30.17 سنة)، الطاقة (662 keV)، ونوع الإشعاع (جاما)." : "Enter half-life (e.g., 30.17 years), energy (662 keV), and radiation type (Gamma)."));

        p.Children.Add(MakeExampleBlock(
            ar ? "مثال: إضافة نظير السيزيوم" : "Example: Adding Cesium Isotope",
            ar ? "الرمز: Cs-137\nالاسم: سيزيوم-137\nnصف العمر: 30.17 سنة\nالطاقة: 661.66 keV\nنوع الإشعاع: جاما (γ)"
               : "Symbol: Cs-137\nName: Cesium-137\nHalf-Life: 30.17 years\nEnergy: 661.66 keV\nRadiation Type: Gamma (γ)"));

        p.Children.Add(MakeWarningBlock(
            ar ? "⚠ تنبيه: حذف نظير مرتبط بمصادر مسجلة غير مسموح. يجب أولاً إزالة أو تعديل المصادر المرتبطة به."
               : "⚠ Warning: Deleting an isotope linked to registered sources is not allowed. You must first remove or modify the linked sources."));
    }

    private void RenderSources()
    {
        var ar = IsArabic();
        var p = RichContentArea;

        p.Children.Add(MakeParagraph(
            ar ? "هذا القسم هو قلب منظومة مسار. هنا يتم تسجيل وتتبع ومراقبة جميع المصادر المشعة الفعلية في المنشأة."
               : "This section is the heart of the Sources system. Here you register, track, and monitor all physical radioactive sources in the facility.",
            16));

        p.Children.Add(MakeSectionHeader("Plus", ar ? "إضافة مصدر جديد" : "Adding a New Source"));

        p.Children.Add(MakeStepBlock("1",
            ar ? "تعريف المصدر" : "Source Definition",
            ar ? "أدخل الكود التشغيلي (مثل CRF-100)، الرقم التسلسلي، الموديل، والشركة المصنعة، وحدد حالة المصدر." 
               : "Enter the operational code (e.g., CRF-100), serial number, model, manufacturer, and set the source status."));

        p.Children.Add(MakeStepBlock("2",
            ar ? "الخصائص الإشعاعية" : "Radiological Properties",
            ar ? "اختر النظير المشع من القائمة، أدخل النشاط الابتدائي ووحدته. النظام يحسب النشاط الحالي تلقائياً."
               : "Select the radioisotope, enter initial activity and its unit. The system calculates current activity automatically."));

        p.Children.Add(MakeStepBlock("3",
            ar ? "اللوجستيات والصورة" : "Logistics & Image",
            ar ? "حدد تاريخ المعايرة/الصنع، الموقع الحالي، وأرفق صورة لوحة البيانات إن توفرت."
               : "Set the calibration/manufacturing date, current location, and attach a data plate image if available."));

        p.Children.Add(MakeExampleBlock(
            ar ? "مثال: تسجيل مصدر سيزيوم" : "Example: Registering a Cesium Source",
            ar ? "كود المصدر: CRF-100\nالنظير: Cs-137\nالنشاط الابتدائي: 3.7 GBq\nتاريخ المعايرة: 2020-01-15\nالموقع: معمل الفيزياء الصحية - غرفة 201\nالحالة: قيد الاستخدام"
               : "Source Code: CRF-100\nIsotope: Cs-137\nInitial Activity: 3.7 GBq\nCalibration Date: 2020-01-15\nLocation: Health Physics Lab - Room 201\nStatus: In Use"));

        p.Children.Add(MakeSectionHeader("Magnify", ar ? "البحث والتصفية" : "Search & Filtering"));

        p.Children.Add(MakeBulletList(
            ar ? "البحث: اكتب كود المصدر أو اسم النظير في صندوق البحث للعثور السريع" : "Search: Type source code or isotope name in the search box for quick lookup",
            ar ? "التصفية بالحالة: استخدم قائمة الفلتر (الكل، قيد الاستخدام، مخزن، نفايات، قيد النقل)" : "Filter by Status: Use the filter dropdown (All, In Use, Stored, Waste, In Transit)",
            ar ? "عرض التفاصيل: اضغط على أيقونة العين 👁 لعرض كامل بيانات المصدر" : "View Details: Click the 👁 eye icon to view the full source data"));

        p.Children.Add(MakeTipBlock(
            ar ? "💡 نصيحة: النظام يحسب النشاط الحالي تلقائياً بناءً على قانون التحلل الإشعاعي A(t) = A₀ × e^(-λt). لا تحتاج لحسابه يدوياً!"
               : "💡 Tip: The system automatically calculates current activity based on the decay law A(t) = A₀ × e^(-λt). No manual calculation needed!"));
    }

    private void RenderBorrowing()
    {
        var ar = IsArabic();
        var p = RichContentArea;

        p.Children.Add(MakeParagraph(
            ar ? "نظام الاستعارة يتيح صرف وإرجاع المصادر المشعة بين الأقسام والكوادر بشكل منظم وموثق."
               : "The Borrowing system enables organized and documented checkout and return of radioactive sources between departments and personnel.",
            16));

        p.Children.Add(MakeSectionHeader("BookPlus", ar ? "إنشاء طلب استعارة" : "Creating a Borrow Request"));

        p.Children.Add(MakeStepBlock("1",
            ar ? "فتح الطلب" : "Open Request",
            ar ? "اضغط على \"طلب استعارة جديد\" واختر المصدر من القائمة المنسدلة." : "Click 'New Borrow Request' and select the source from the dropdown."));

        p.Children.Add(MakeStepBlock("2",
            ar ? "تحديد التفاصيل" : "Specify Details",
            ar ? "اختر اسم المستعير، اكتب الغرض من الاستعارة، وحدد تاريخ الإرجاع المتوقع." : "Select the borrower name, write the purpose, and set the expected return date."));

        p.Children.Add(MakeStepBlock("3",
            ar ? "الموافقة والتسليم" : "Approval & Delivery",
            ar ? "المشرف يراجع الطلب ← يوافق أو يرفض ← بعد الموافقة يتم تسليم المصدر ← يُسجل كـ \"مُسلَّم\"."
               : "Supervisor reviews request → Approves or Rejects → After approval, source is delivered → Marked as 'Delivered'."));

        p.Children.Add(MakeStepBlock("4",
            ar ? "الإرجاع" : "Return",
            ar ? "عند إعادة المصدر، يتم تسجيل \"تأكيد الاسترجاع\" مع تحديد الشخص المستلم. يسترجع المصدر حالته السابقة."
               : "When returning the source, confirm 'Returned' with the receiving person specified. The source reverts to its previous status."));

        p.Children.Add(MakeExampleBlock(
            ar ? "مثال: دورة استعارة كاملة" : "Example: Full Borrowing Cycle",
            ar ? "المستخدم ← ينشئ طلب استعارة لمصدر CRF-100\nالمشرف ← يوافق على الطلب\nالمسؤول ← يسلّم المصدر (الحالة: مُسلَّم)\nالمستخدم ← يعيد المصدر بعد الاستخدام\nالمسؤول ← يؤكد الاسترجاع (الحالة: مُرتجع)"
               : "User → Creates borrow request for source CRF-100\nSupervisor → Approves the request\nOfficer → Delivers the source (Status: Delivered)\nUser → Returns the source after use\nOfficer → Confirms return (Status: Returned)"));

        p.Children.Add(MakeWarningBlock(
            ar ? "⚠ تنبيه: الطلبات المتأخرة عن تاريخ الإرجاع المتوقع تظهر في لوحة القيادة كتنبيهات عاجلة باللون الأحمر."
               : "⚠ Warning: Requests overdue past the expected return date appear on the Dashboard as urgent red alerts."));
    }

    private void RenderReports()
    {
        var ar = IsArabic();
        var p = RichContentArea;

        p.Children.Add(MakeParagraph(
            ar ? "مركز التقارير يتيح لك استخراج مستندات رسمية ودقيقة عن حالة المصادر والنشاط الإشعاعي والاستعارة والصيانة."
               : "The Reports Center lets you generate official, accurate documents about source status, activity, borrowing, and maintenance.",
            16));

        p.Children.Add(MakeSectionHeader("FileChart", ar ? "أنواع التقارير المتاحة" : "Available Report Types"));

        p.Children.Add(MakeBulletList(
            ar ? "📋 تقرير الجرد: قائمة كاملة بجميع المصادر المسجلة مع بياناتها التفصيلية" : "📋 Inventory Report: Complete list of all registered sources with detailed data",
            ar ? "🔄 تقرير الاستعارة: السجل التاريخي لعمليات الصرف والإرجاع" : "🔄 Borrowing Report: Historical log of checkouts and returns",
            ar ? "☢ تقرير النشاط: عرض النشاط الإشعاعي الحالي لكل مصدر" : "☢ Activity Report: Current radioactive activity for each source",
            ar ? "⚠ تقرير المصادر المنخفضة: المصادر التي اضمحل نشاطها بشكل كبير" : "⚠ Low Activity Report: Sources with significantly decayed activity",
            ar ? "📅 تنبيهات المعايرة: المصادر القريبة أو المتجاوزة لتاريخ المعايرة" : "📅 Calibration Alerts: Sources near or past calibration date",
            ar ? "📑 التقرير العام الشامل: يجمع كل التقارير أعلاه في ملف واحد" : "📑 General Report: Combines all above reports in a single file"));

        p.Children.Add(MakeSectionHeader("Download", ar ? "تصدير التقارير" : "Exporting Reports"));

        p.Children.Add(MakeStepBlock("1",
            ar ? "اختيار التقرير" : "Select Report",
            ar ? "اضغط على زر التقرير المطلوب من الشريط العلوي (مثل تقرير الجرد أو النشاط)." : "Click the desired report button from the top bar (e.g., Inventory or Activity)."));

        p.Children.Add(MakeStepBlock("2",
            ar ? "مراجعة البيانات" : "Review Data",
            ar ? "سيظهر الجدول بالبيانات المطلوبة. يمكنك مراجعتها والتأكد من صحتها." : "The table displays the requested data. Review it to ensure accuracy."));

        p.Children.Add(MakeStepBlock("3",
            ar ? "التصدير" : "Export",
            ar ? "اضغط على \"تصدير PDF\" أو \"تصدير Excel\" لتحميل الملف وحفظه على جهازك." : "Click 'Export PDF' or 'Export Excel' to download and save the file on your computer."));

        p.Children.Add(MakeTipBlock(
            ar ? "💡 نصيحة: استخدم \"التقرير العام الشامل\" 📑 عندما تحتاج لتقديم تقرير واحد شامل للجهات الرقابية يحتوي على جميع البيانات."
               : "💡 Tip: Use the 'General Report' 📑 when you need to submit a single comprehensive report to regulatory authorities containing all data."));
    }

    private void RenderSettings()
    {
        var ar = IsArabic();
        var p = RichContentArea;

        p.Children.Add(MakeParagraph(
            ar ? "قسم الإعدادات والمستخدمين يتيح لمدير النظام التحكم الكامل في حسابات المستخدمين والصلاحيات والنسخ الاحتياطي."
               : "The Settings & Users section allows the system administrator full control over user accounts, permissions, and backups.",
            16));

        p.Children.Add(MakeSectionHeader("AccountGroup", ar ? "إدارة المستخدمين" : "User Management"));

        p.Children.Add(MakeStepBlock("1",
            ar ? "إنشاء حساب" : "Create Account",
            ar ? "اضغط \"➕ إضافة مستخدم جديد\" → أدخل الاسم الكامل، اسم المستخدم، كلمة المرور، والبريد الإلكتروني."
               : "Click '➕ Add New User' → Enter full name, username, password, and email."));

        p.Children.Add(MakeStepBlock("2",
            ar ? "تحديد الصلاحيات" : "Set Permissions",
            ar ? "اختر الدور (مدير النظام أو مستخدم عادي) ← حدد الأقسام المسموح الوصول إليها ← فعّل/ألغِ صلاحية التعديل."
               : "Select role (Admin or User) → Choose accessible sections → Enable/disable edit permission."));

        p.Children.Add(MakeExampleBlock(
            ar ? "مثال: إنشاء حساب لفني الإشعاع" : "Example: Creating a Radiation Technician Account",
            ar ? "الاسم: أحمد محمد\nاسم المستخدم: ahmed.m\nالدور: مستخدم\nالصلاحيات: ✅ المصادر ✅ الاستعارة ✅ التقارير ✅ الحاسبة\n           ❌ النظائر ❌ المستخدمين ❌ الإعدادات"
               : "Name: Ahmed Mohamed\nUsername: ahmed.m\nRole: User\nPermissions: ✅ Sources ✅ Borrowing ✅ Reports ✅ Calculator\n             ❌ Isotopes ❌ Users ❌ Settings"));

        p.Children.Add(MakeSectionHeader("CloudUpload", ar ? "النسخ الاحتياطي" : "Backup & Restore"));

        p.Children.Add(MakeBulletList(
            ar ? "النسخ التلقائي: يمكن تفعيله (يومي/أسبوعي/شهري) مع تحديد مجلد الحفظ" : "Auto Backup: Can be enabled (Daily/Weekly/Monthly) with save folder selection",
            ar ? "النسخ اليدوي: اضغط \"إنشاء نسخة احتياطية الآن\" لعمل نسخة فورية" : "Manual Backup: Click 'Create Backup Now' for an instant backup",
            ar ? "الاستعادة: اضغط \"استعادة نسخة احتياطية\" واختر ملف النسخة المطلوبة" : "Restore: Click 'Restore Backup' and select the desired backup file"));

        p.Children.Add(MakeSectionHeader("Brightness6", ar ? "المظهر واللغة" : "Theme & Language"));

        p.Children.Add(MakeBulletList(
            ar ? "الوضع الداكن/الفاتح: اضغط على أيقونة الشمس/القمر في القائمة الجانبية للتبديل الفوري" : "Dark/Light Mode: Click the sun/moon icon in the sidebar for instant toggle",
            ar ? "اللغة: اختر العربية أو الإنجليزية من صفحة الإعدادات — يتم تبديل كل النصوص فوراً" : "Language: Choose Arabic or English from Settings — all text switches instantly"));

        p.Children.Add(MakeWarningBlock(
            ar ? "⚠ تنبيه: عملية استعادة النسخة الاحتياطية ستستبدل جميع البيانات الحالية. تأكد من حفظ نسخة حديثة قبل الاستعادة."
               : "⚠ Warning: Restoring a backup will replace all current data. Make sure you have a recent backup saved before restoring."));
    }
}
