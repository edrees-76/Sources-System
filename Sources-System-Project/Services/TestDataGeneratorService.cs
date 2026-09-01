#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Models;

namespace Sources.Services;

/// <summary>
/// نتيجة عملية توليد البيانات التجريبية
/// </summary>
public class TestDataGenerationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TotalLocations { get; set; }
    public int AddedLocations { get; set; }
    public int TotalSources { get; set; }
    public int MultiIsotopeSources { get; set; }
    public int WarningAlertSources { get; set; }
    public int CriticalAlertSources { get; set; }
    public int TotalBorrowRequests { get; set; }
    public int ReturnedBorrows { get; set; }
    public int DeliveredBorrows { get; set; }
    public int OverdueBorrows { get; set; }
    public int PendingOrApprovedBorrows { get; set; }
}

/// <summary>
/// أداة توليد بيانات تجريبية واقعية وضخمة (Debug-only)
/// </summary>
public static class TestDataGeneratorService
{
    public static async Task<TestDataGenerationResult> GenerateRealisticDataAsync(
        IDbContextFactory<AppDbContext> dbFactory,
        IDecayCalculationService decayService,
        IAlertService? alertService = null,
        IUserService? userService = null)
    {
        return await Task.Run(() =>
        {
            var result = new TestDataGenerationResult();
            var random = new Random(42); // Seed ثابت لتكرارية معقولة أو شبه عشوائية

            using var db = dbFactory.CreateDbContext();
            using var transaction = db.Database.BeginTransaction();

            try
            {
                var currentUserId = userService?.CurrentUser?.Id ?? db.Users.Select(u => (Guid?)u.Id).FirstOrDefault();

                // ─────────────────────────────────────────────────────────────
                // 1. المواقع (Locations) — إجمالي 20 موقعاً
                // ─────────────────────────────────────────────────────────────
                var existingLocations = db.Locations.Where(l => !l.IsDeleted).ToList();
                var candidateLocations = new List<Location>
                {
                    new() { LocationName = "المخزن الرئيسي", LocationType = "Storage", Building = "المبنى A", Room = "غرفة 101", ResponsiblePerson = "المشرف العام" },
                    new() { LocationName = "معمل القياسات الإشعاعية", LocationType = "Lab", Building = "المبنى B", Room = "غرفة 201", ResponsiblePerson = "رئيس المعمل" },
                    new() { LocationName = "قسم الطب النووي", LocationType = "Hospital", Building = "المبنى C", Room = "غرفة 301", ResponsiblePerson = "الطبيب المختص" },
                    new() { LocationName = "مختبر التحليل الطيفي غاما", LocationType = "Lab", Building = "مبنى الفيزياء النووية", Room = "قاعة 102", ResponsiblePerson = "د. أحمد الساعدي" },
                    new() { LocationName = "معمل القياسات المتقدمة HPGe", LocationType = "Lab", Building = "المبنى B", Room = "غرفة 204", ResponsiblePerson = "أ. محمود الفرجاني" },
                    new() { LocationName = "مخزن المصادر عالية الشدة", LocationType = "Storage", Building = "المبنى المحصن A", Room = "القبو المحصن B1", ResponsiblePerson = "م. خالد الورفلي" },
                    new() { LocationName = "مخزن النفايات المشعة المؤقت", LocationType = "Storage", Building = "مبنى السلامة الإشعاعية", Room = "غرفة 10", ResponsiblePerson = "فني ناصر الترهوني" },
                    new() { LocationName = "وحدة المعالجة الإشعاعية الكثبية", LocationType = "Hospital", Building = "مركز الأورام التخصصي", Room = "غرفة 112", ResponsiblePerson = "د. مروان بن علي" },
                    new() { LocationName = "قسم الطب النووي والتشخيص الجزيئي", LocationType = "Hospital", Building = "المستشفى الجامعي", Room = "جناح 4", ResponsiblePerson = "د. سارة الكيلاني" },
                    new() { LocationName = "عيادة الغدة الدرقية والنظائر المشعة", LocationType = "Clinic", Building = "المركز التخصصي", Room = "عيادة 5", ResponsiblePerson = "د. هناء الزوي" },
                    new() { LocationName = "مختبر المعايرة الدقيقة والجرعات", LocationType = "Lab", Building = "مبنى المعايرة الوطنية", Room = "قاعة 3", ResponsiblePerson = "د. عبد السلام الزنتاني" },
                    new() { LocationName = "معمل أبحاث وتطبيقات النظائر", LocationType = "Lab", Building = "كلية العلوم", Room = "مختبر 305", ResponsiblePerson = "د. فاتح القماطي" },
                    new() { LocationName = "مخزن كواشف الفحص الإشعاعي", LocationType = "Storage", Building = "مبنى الدعم الفني", Room = "غرفة 15", ResponsiblePerson = "م. هشام المقريف" },
                    new() { LocationName = "وحدة التصوير البوزيتروني PET/CT", LocationType = "Hospital", Building = "مركز التشخيص المتطور", Room = "جناح PET", ResponsiblePerson = "د. إبراهيم البوسيفي" },
                    new() { LocationName = "عيادة العلاج الإشعاعي الموضعي", LocationType = "Clinic", Building = "مجمع العيادات التخصصية", Room = "عيادة 12", ResponsiblePerson = "د. رانية الشريف" },
                    new() { LocationName = "مختبر القياسات الحيوية الإشعاعية", LocationType = "Lab", Building = "معهد البحوث النووية", Room = "معمل 202", ResponsiblePerson = "أ. فوزي دربي" },
                    new() { LocationName = "غرفة التخزين الحصين للمصادر القياسية", LocationType = "Storage", Building = "المبنى المركزي", Room = "خزانة C-01", ResponsiblePerson = "م. طارق العبيدي" },
                    new() { LocationName = "وحدة الصيدلة الإشعاعية والتحضير", LocationType = "Hospital", Building = "مستشفى الأمل", Room = "الغرفة النظيفة 1", ResponsiblePerson = "د. ليلى المجبري" },
                    new() { LocationName = "عيادة الرقابة وقياس الجرعات الشخصية", LocationType = "Clinic", Building = "المركز الطبي", Room = "عيادة الرقابة", ResponsiblePerson = "م. أيمن القمودي" },
                    new() { LocationName = "معمل الرقابة البيئية الإشعاعية", LocationType = "Lab", Building = "هيئة السلامة والبيئة", Room = "مختبر 108", ResponsiblePerson = "أ. وليد المهدوي" }
                };

                int addedLocationsCount = 0;
                foreach (var cand in candidateLocations)
                {
                    if (existingLocations.Count >= 20) break;

                    var match = existingLocations.FirstOrDefault(l => l.LocationName == cand.LocationName);
                    if (match == null)
                    {
                        cand.AddedBy = currentUserId;
                        db.Locations.Add(cand);
                        existingLocations.Add(cand);
                        addedLocationsCount++;
                    }
                }
                db.SaveChanges();
                result.TotalLocations = existingLocations.Count;
                result.AddedLocations = addedLocationsCount;

                var allLocations = existingLocations.Take(20).ToList();

                // ─────────────────────────────────────────────────────────────
                // تحميل البيانات الأساسية: النظائر والوحدات
                // ─────────────────────────────────────────────────────────────
                var allIsotopes = db.Radioisotopes.Where(r => !r.IsDeleted).ToList();
                if (allIsotopes.Count == 0)
                {
                    throw new InvalidOperationException("لم يتم العثور على نظائر مشعة في قاعدة البيانات.");
                }

                var allUnits = db.ActivityUnits.ToList();
                var uCiUnit = allUnits.FirstOrDefault(u => u.UnitSymbol == "µCi" || u.UnitSymbol == "uCi") ?? allUnits.First();
                var mCiUnit = allUnits.FirstOrDefault(u => u.UnitSymbol == "mCi") ?? uCiUnit;
                var ciUnit = allUnits.FirstOrDefault(u => u.UnitSymbol == "Ci") ?? mCiUnit;
                var bqUnit = allUnits.FirstOrDefault(u => u.UnitSymbol == "Bq") ?? mCiUnit;

                // نويدات قصيرة العمر مخصصة لحالات التنبيهات (Warning / Critical)
                var alertIsotopeSymbols = new HashSet<string> { "I-131", "Ir-192", "Se-75", "Co-57", "Tc-99m", "F-18", "Tl-208" };
                var shortLivedIsotopes = allIsotopes.Where(i => alertIsotopeSymbols.Contains(i.Symbol)).ToList();
                if (shortLivedIsotopes.Count == 0) shortLivedIsotopes = allIsotopes;

                // ─────────────────────────────────────────────────────────────
                // 2. المصادر (Sources) — إجمالي 300 مصدر
                // ─────────────────────────────────────────────────────────────
                var generatedSources = new List<Source>();
                var generatedSourceIsotopes = new List<SourceIsotope>();
                var generatedHistories = new List<SourceLocationHistory>();

                // تحديد أوزان المواقع (توزيع غير متساوٍ واقعي)
                int[] locationWeights = new int[] { 30, 25, 25, 20, 20, 18, 18, 16, 16, 14, 14, 12, 12, 10, 10, 10, 10, 10, 5, 5 };

                // إنشاء جدول اختيار المواقع وفق الأوزان
                var weightedLocationIndices = new List<int>();
                for (int li = 0; li < allLocations.Count; li++)
                {
                    int weight = (li < locationWeights.Length) ? locationWeights[li] : 10;
                    for (int w = 0; w < weight; w++)
                    {
                        weightedLocationIndices.Add(li);
                    }
                }

                // العثور على بادئة الأكواد الحرة (SRC-0001 إلى SRC-0300)
                var existingCodes = db.Sources.Select(s => s.SourceCode).ToHashSet();

                int codeCounter = 1;
                for (int i = 0; i < 300; i++)
                {
                    string sourceCode;
                    do
                    {
                        sourceCode = $"SRC-{codeCounter:D4}";
                        codeCounter++;
                    } while (existingCodes.Contains(sourceCode));
                    existingCodes.Add(sourceCode);

                    var locIndex = weightedLocationIndices[random.Next(weightedLocationIndices.Count)];
                    var location = allLocations[locIndex];

                    var source = new Source
                    {
                        Id = Guid.NewGuid(),
                        SourceCode = sourceCode,
                        SerialNumber = $"SN-202{random.Next(1, 7)}-{random.Next(1000, 9999)}",
                        Manufacturer = GetRandomManufacturer(random),
                        Model = $"MOD-{random.Next(100, 999)}",
                        LocationId = location.Id,
                        AddedBy = currentUserId,
                        CreatedAt = DateTime.Now.AddDays(-random.Next(10, 365 * 2))
                    };

                    if (i >= 250 && i < 275)
                    {
                        // ─── المجموعة (b): 25 مصدراً في حالة "تحذير" (Warning: 5.3 إلى 5.8 فترة نصف عمر) ───
                        var iso = shortLivedIsotopes[random.Next(shortLivedIsotopes.Count)];
                        source.RadioisotopeId = iso.Id;
                        source.Radioisotope = iso;
                        source.HasDetailedIsotopes = false;
                        source.Status = "Storage"; // تبدأ في المخزن وتتغير لاحقاً إذا ارتبطت باستعارة نشطة

                        // اختيار وحدة مناسبة للنويدة
                        var (initVal, unit) = GetRealisticActivity(iso, random, uCiUnit, mCiUnit, ciUnit, bqUnit);
                        source.InitialActivityValue = initVal;
                        source.InitialActivityUnitId = unit.Id;
                        source.InitialActivityUnit = unit;
                        source.CurrentActivityUnitId = unit.Id;
                        source.CurrentActivityUnit = unit;

                        double halfLifeSec = decayService.ConvertTimeToSeconds(iso.HalfLife, iso.HalfLifeUnit);
                        double factor = 5.3 + (random.NextDouble() * 0.5); // 5.3 إلى 5.8
                        source.CalibrationDate = DateTime.Now.AddSeconds(-factor * halfLifeSec);

                        // حساب النشاط الحالي الفعلي
                        double elapsedSec = Math.Max(0, (DateTime.Now - source.CalibrationDate).TotalSeconds);
                        double decayFactor = Math.Pow(0.5, elapsedSec / halfLifeSec);
                        source.CurrentActivityValue = source.InitialActivityValue * decayFactor;

                        result.WarningAlertSources++;
                    }
                    else if (i >= 275)
                    {
                        // ─── المجموعة (c): 25 مصدراً في حالة "حرجة" (Critical: 6.2 إلى 8.0 فترة نصف عمر) ───
                        var iso = shortLivedIsotopes[random.Next(shortLivedIsotopes.Count)];
                        source.RadioisotopeId = iso.Id;
                        source.Radioisotope = iso;
                        source.HasDetailedIsotopes = false;
                        source.Status = "Storage"; // تبدأ في المخزن وتتغير لاحقاً إذا ارتبطت باستعارة نشطة

                        var (initVal, unit) = GetRealisticActivity(iso, random, uCiUnit, mCiUnit, ciUnit, bqUnit);
                        source.InitialActivityValue = initVal;
                        source.InitialActivityUnitId = unit.Id;
                        source.InitialActivityUnit = unit;
                        source.CurrentActivityUnitId = unit.Id;
                        source.CurrentActivityUnit = unit;

                        double halfLifeSec = decayService.ConvertTimeToSeconds(iso.HalfLife, iso.HalfLifeUnit);
                        double factor = 6.2 + (random.NextDouble() * 1.8); // 6.2 إلى 8.0
                        source.CalibrationDate = DateTime.Now.AddSeconds(-factor * halfLifeSec);

                        double elapsedSec = Math.Max(0, (DateTime.Now - source.CalibrationDate).TotalSeconds);
                        double decayFactor = Math.Pow(0.5, elapsedSec / halfLifeSec);
                        source.CurrentActivityValue = source.InitialActivityValue * decayFactor;

                        result.CriticalAlertSources++;
                    }
                    else
                    {
                        // ─── المجموعة (a): 250 مصدراً عادياً (بين 0.05 إلى 2.5 فترة نصف عمر، بحد أقصى 3 سنوات) ───
                        // الحالة الافتراضية: 90% Storage، 5% Waste، 5% Transfer (تتحول إلى InUse لاحقاً فقط للاستعارات النشطة)
                        int statusRoll = random.Next(100);
                        if (statusRoll < 90) source.Status = "Storage";
                        else if (statusRoll < 95) source.Status = "Waste";
                        else source.Status = "Transfer";

                        // هل المصدر متعدد النويدات؟ (~60 مصدر من أول 60 في المجموعة a)
                        if (i < 60)
                        {
                            source.HasDetailedIsotopes = true;
                            result.MultiIsotopeSources++;

                            // اختيار 2 إلى 3 نويدات فريدة
                            int isotopeCount = random.Next(2, 4);
                            var selectedIsotopes = allIsotopes.OrderBy(_ => random.Next()).Take(isotopeCount).ToList();
                            source.RadioisotopeId = selectedIsotopes[0].Id;
                            source.Radioisotope = selectedIsotopes[0];

                            // وحدة النشاط الأساسية للمصدر
                            var sourceUnit = mCiUnit;
                            source.InitialActivityUnitId = sourceUnit.Id;
                            source.InitialActivityUnit = sourceUnit;
                            source.CurrentActivityUnitId = sourceUnit.Id;
                            source.CurrentActivityUnit = sourceUnit;

                            double totalInitialBq = 0;
                            double totalCurrentBq = 0;

                            foreach (var iso in selectedIsotopes)
                            {
                                var (isoInitVal, isoUnit) = GetRealisticActivity(iso, random, uCiUnit, mCiUnit, ciUnit, bqUnit);
                                double isoHalfLifeSec = decayService.ConvertTimeToSeconds(iso.HalfLife, iso.HalfLifeUnit);

                                // احتساب مدة انقضاء دقيقة بين 0.05 و 2.5 فترة نصف عمر بالثواني
                                double isoFactor = 0.05 + (random.NextDouble() * 2.45);
                                double isoElapsedSec = isoFactor * isoHalfLifeSec;
                                double maxAllowedSec = 3.0 * 365.25 * 86400.0;
                                if (isoElapsedSec > maxAllowedSec) isoElapsedSec = maxAllowedSec;

                                var isoCalibDate = DateTime.Now.AddSeconds(-isoElapsedSec);
                                double isoDecayFactor = Math.Pow(0.5, isoElapsedSec / isoHalfLifeSec);
                                double isoCurrentVal = isoInitVal * isoDecayFactor;

                                var si = new SourceIsotope
                                {
                                    Id = Guid.NewGuid(),
                                    SourceId = source.Id,
                                    RadioisotopeId = iso.Id,
                                    InitialActivityValue = isoInitVal,
                                    ActivityUnitId = isoUnit.Id,
                                    CurrentActivityValue = isoCurrentVal,
                                    CalibrationDate = isoCalibDate,
                                    Notes = $"نويدة فرعية للمصدر {source.SourceCode}"
                                };
                                generatedSourceIsotopes.Add(si);
                                source.SourceIsotopes.Add(si);

                                totalInitialBq += isoInitVal * isoUnit.ConversionToBq;
                                totalCurrentBq += isoCurrentVal * isoUnit.ConversionToBq;
                            }

                            source.CalibrationDate = source.SourceIsotopes.Min(si => si.CalibrationDate ?? DateTime.Now);
                            source.InitialActivityValue = totalInitialBq / sourceUnit.ConversionToBq;
                            source.CurrentActivityValue = totalCurrentBq / sourceUnit.ConversionToBq;
                        }
                        else
                        {
                            source.HasDetailedIsotopes = false;
                            var iso = allIsotopes[random.Next(allIsotopes.Count)];
                            source.RadioisotopeId = iso.Id;
                            source.Radioisotope = iso;

                            double halfLifeSec = decayService.ConvertTimeToSeconds(iso.HalfLife, iso.HalfLifeUnit);
                            double factor = 0.05 + (random.NextDouble() * 2.45);
                            double elapsedSec = factor * halfLifeSec;
                            double maxAllowedSec = 3.0 * 365.25 * 86400.0;
                            if (elapsedSec > maxAllowedSec) elapsedSec = maxAllowedSec;

                            source.CalibrationDate = DateTime.Now.AddSeconds(-elapsedSec);

                            var (initVal, unit) = GetRealisticActivity(iso, random, uCiUnit, mCiUnit, ciUnit, bqUnit);
                            source.InitialActivityValue = initVal;
                            source.InitialActivityUnitId = unit.Id;
                            source.InitialActivityUnit = unit;
                            source.CurrentActivityUnitId = unit.Id;
                            source.CurrentActivityUnit = unit;

                            double decayFactor = Math.Pow(0.5, elapsedSec / halfLifeSec);
                            source.CurrentActivityValue = source.InitialActivityValue * decayFactor;
                        }
                    }

                    generatedSources.Add(source);

                    // سجل تاريخ الموقع الأولي
                    generatedHistories.Add(new SourceLocationHistory
                    {
                        Id = Guid.NewGuid(),
                        SourceId = source.Id,
                        LocationId = source.LocationId,
                        PreviousLocationId = null,
                        MovedAt = source.CreatedAt
                    });
                }

                // حفظ المصادر وما يرتبط بها من SourceIsotopes بالدفعات (Batches of 50)
                int batchSize = 50;
                for (int b = 0; b < generatedSources.Count; b += batchSize)
                {
                    var chunk = generatedSources.Skip(b).Take(batchSize).ToList();
                    db.Sources.AddRange(chunk);
                    db.SaveChanges();
                }

                for (int b = 0; b < generatedHistories.Count; b += batchSize)
                {
                    var chunk = generatedHistories.Skip(b).Take(batchSize).ToList();
                    db.SourceLocationHistories.AddRange(chunk);
                    db.SaveChanges();
                }

                result.TotalSources = generatedSources.Count;

                // ─────────────────────────────────────────────────────────────
                // 3. طلبات الاستعارة (BorrowRequests) — إجمالي 100 طلب
                // ─────────────────────────────────────────────────────────────
                var generatedBorrowRequests = new List<BorrowRequest>();

                var arabicBorrowers = new[]
                {
                    "د. طارق السويح", "أ. هند المجبري", "م. عماد الفيتوري", "د. عبد الرحمن بن موسى",
                    "أ. سمية التاجوري", "م. عمر القروي", "د. نادية الترهوني", "أ. خالد الدرسي",
                    "م. فؤاد الكاتب", "د. أسامة الزنتاني", "أ. وفاء الشيباني", "م. زياد النعاس",
                    "د. ريم المقريف", "أ. سالم الشريف", "م. كمال الصويعي", "د. مصطفى العيساوي",
                    "أ. منى البكوش", "م. عبد الله الغرياني", "د. خديجة السنوسي", "أ. نوري القماطي"
                };

                var arabicPurposes = new[]
                {
                    "معايرة دورية لجهاز قياس الجرعات الإشعاعية",
                    "تدريب طلبة الدراسات العليا على القياسات الطيفية",
                    "فحص الجودة النوعية لكاشف أشعة غاما الميداني",
                    "إجراء تجارب التحلل الإشعاعي للنويدات في المختبر",
                    "معايرة كواشف الجرمانيوم عالي النقاوة HPGe",
                    "دراسة كفاءة مواد التدريع الإشعاعي ضد مصادر غاما",
                    "تدريب فنيي الحماية الإشعاعية على تداول المصادر المغلقة",
                    "فحص ومعايرة أجهزة المسح الإشعاعي المحمولة",
                    "بحث علمي حول التشتت الإشعاعي وزوايا الانبعاث",
                    "اختبار استجابة أجهزة الرصد البيئي المستمر"
                };

                var usedSourceIdsForActiveBorrow = new HashSet<Guid>();

                // أ) 70 طلباً مرتجعاً بالكامل (Status = "Returned")
                for (int i = 0; i < 70; i++)
                {
                    var src = generatedSources[random.Next(generatedSources.Count)];
                    if (src.Status != "Waste" && src.Status != "Transfer")
                    {
                        src.Status = "Storage"; // المصدر المرتجع يعود للمخزن
                    }

                    var reqDate = DateTime.Now.AddDays(-random.Next(60, 300));
                    var appDate = reqDate.AddHours(random.Next(2, 24));
                    var delDate = appDate.AddHours(random.Next(1, 12));
                    var expDate = delDate.AddDays(random.Next(3, 15));
                    var actDate = expDate.AddDays(random.Next(-2, 4));

                    var req = new BorrowRequest
                    {
                        Id = Guid.NewGuid(),
                        SourceId = src.Id,
                        BorrowerName = arabicBorrowers[random.Next(arabicBorrowers.Length)],
                        Purpose = arabicPurposes[random.Next(arabicPurposes.Length)],
                        RequestDate = reqDate,
                        ApprovalDate = appDate,
                        DeliveryDate = delDate,
                        ExpectedReturnDate = expDate,
                        ActualReturnDate = actDate,
                        Status = "Returned",
                        AddedBy = currentUserId,
                        Notes = "تم الإرجاع بحالة سليمة ومطابقة القياسات."
                    };
                    generatedBorrowRequests.Add(req);
                    result.ReturnedBorrows++;
                }

                // مصادر Storage المتاحة للاستعارات النشطة والمتأخرة
                var availableSourcesForActiveBorrow = generatedSources.Where(s => s.Status == "Storage").ToList();

                // ب) 15 طلباً مستلماً ونشطاً (Status = "Delivered" — غير متأخر، ExpectedReturnDate في المستقبل)
                // الالتزام التام بفهرس التفرد: مصدر واحد لكل استعارة نشطة
                int deliveredCount = 0;
                foreach (var src in availableSourcesForActiveBorrow)
                {
                    if (deliveredCount >= 15) break;
                    if (usedSourceIdsForActiveBorrow.Contains(src.Id)) continue;

                    usedSourceIdsForActiveBorrow.Add(src.Id);
                    src.Status = "InUse"; // تحديث حالة المصدر ليكون قيد الاستخدام
                    var reqDate = DateTime.Now.AddDays(-random.Next(1, 10));
                    var appDate = reqDate.AddHours(random.Next(1, 12));
                    var delDate = appDate.AddHours(random.Next(1, 6));
                    var expDate = DateTime.Today.AddDays(random.Next(3, 15)); // تاريخ في المستقبل

                    var req = new BorrowRequest
                    {
                        Id = Guid.NewGuid(),
                        SourceId = src.Id,
                        BorrowerName = arabicBorrowers[random.Next(arabicBorrowers.Length)],
                        Purpose = arabicPurposes[random.Next(arabicPurposes.Length)],
                        RequestDate = reqDate,
                        ApprovalDate = appDate,
                        DeliveryDate = delDate,
                        ExpectedReturnDate = expDate,
                        ActualReturnDate = null,
                        Status = "Delivered",
                        AddedBy = currentUserId,
                        Notes = "استعارة جارية قيد العمل بالمختبر."
                    };
                    generatedBorrowRequests.Add(req);
                    deliveredCount++;
                    result.DeliveredBorrows++;
                }

                // ج) 10 طلبات متأخرة صراحةً (Status = "Overdue" — ExpectedReturnDate في الماضي)
                int overdueCount = 0;
                foreach (var src in availableSourcesForActiveBorrow)
                {
                    if (overdueCount >= 10) break;
                    if (usedSourceIdsForActiveBorrow.Contains(src.Id)) continue;

                    usedSourceIdsForActiveBorrow.Add(src.Id);
                    src.Status = "InUse"; // تحديث حالة المصدر ليكون قيد الاستخدام ومتأخراً
                    var reqDate = DateTime.Now.AddDays(-random.Next(25, 70));
                    var appDate = reqDate.AddHours(random.Next(1, 12));
                    var delDate = appDate.AddHours(random.Next(1, 6));
                    var expDate = DateTime.Today.AddDays(-random.Next(3, 20)); // تاريخ في الماضي

                    var req = new BorrowRequest
                    {
                        Id = Guid.NewGuid(),
                        SourceId = src.Id,
                        BorrowerName = arabicBorrowers[random.Next(arabicBorrowers.Length)],
                        Purpose = arabicPurposes[random.Next(arabicPurposes.Length)],
                        RequestDate = reqDate,
                        ApprovalDate = appDate,
                        DeliveryDate = delDate,
                        ExpectedReturnDate = expDate,
                        ActualReturnDate = null,
                        Status = "Overdue",
                        AddedBy = currentUserId,
                        Notes = "تنبيه: تجاوز موعد الإرجاع المحدد، تم التواصل مع المستعير."
                    };
                    generatedBorrowRequests.Add(req);
                    overdueCount++;
                    result.OverdueBorrows++;
                }

                // د) 5 طلبات معلقة أو قيد الموافقة (3 Pending + 2 Approved)
                var storageSources = generatedSources.Where(s => s.Status == "Storage").ToList();
                for (int i = 0; i < 5; i++)
                {
                    var src = (storageSources.Count > i) ? storageSources[i] : generatedSources[random.Next(generatedSources.Count)];
                    var reqDate = DateTime.Now.AddDays(-random.Next(0, 3));
                    bool isApproved = (i >= 3);

                    var req = new BorrowRequest
                    {
                        Id = Guid.NewGuid(),
                        SourceId = src.Id,
                        BorrowerName = arabicBorrowers[random.Next(arabicBorrowers.Length)],
                        Purpose = arabicPurposes[random.Next(arabicPurposes.Length)],
                        RequestDate = reqDate,
                        ApprovalDate = isApproved ? reqDate.AddHours(4) : null,
                        DeliveryDate = null,
                        ExpectedReturnDate = DateTime.Today.AddDays(random.Next(7, 20)),
                        ActualReturnDate = null,
                        Status = isApproved ? "Approved" : "Pending",
                        AddedBy = currentUserId,
                        Notes = isApproved ? "تمت الموافقة وفي انتظار تسليم المصدر للمستعير." : "طلب استعارة جديد بانتظار اعتماد المشرف."
                    };
                    generatedBorrowRequests.Add(req);
                    result.PendingOrApprovedBorrows++;
                }

                // حفظ طلبات الاستعارة بالدفعات
                for (int b = 0; b < generatedBorrowRequests.Count; b += batchSize)
                {
                    var chunk = generatedBorrowRequests.Skip(b).Take(batchSize).ToList();
                    db.BorrowRequests.AddRange(chunk);
                    db.SaveChanges();
                }

                result.TotalBorrowRequests = generatedBorrowRequests.Count;

                // اعتماد العملية ككل في قاعدة البيانات
                transaction.Commit();

                // تحديث وتوليد التنبيهات الذكية لتظهر فوراً
                alertService?.GenerateAlerts();

                result.Success = true;
                result.Message = "تم توليد البيانات التجريبية بنجاح تام.";
                return result;
            }
            catch (Exception ex)
            {
                try { transaction.Rollback(); } catch { }
                result.Success = false;
                result.Message = $"فشل توليد البيانات التجريبية: {ex.Message}{(ex.InnerException != null ? " -> " + ex.InnerException.Message : "")}";
                return result;
            }
        });
    }

    private static string GetRandomManufacturer(Random random)
    {
        var manufacturers = new[]
        {
            "Eckert & Ziegler (Germany)",
            "Amersham International (UK)",
            "Isotope Products Laboratories (USA)",
            "Polatom (Poland)",
            "CERCA (France)",
            "Atomenergomash (Russia)",
            "NTP Radioisotopes (South Africa)",
            "Nordion Inc. (Canada)"
        };
        return manufacturers[random.Next(manufacturers.Length)];
    }

    private static (double Value, ActivityUnit Unit) GetRealisticActivity(
        Radioisotope iso,
        Random random,
        ActivityUnit uCiUnit,
        ActivityUnit mCiUnit,
        ActivityUnit ciUnit,
        ActivityUnit bqUnit)
    {
        var type = iso.RadiationType ?? string.Empty;

        if (type.Contains("Alpha", StringComparison.OrdinalIgnoreCase))
        {
            // مصادر ألفا (Am-241, Pu-239, Ra-226): عادة بالميكروكوري
            double val = Math.Round(0.5 + (random.NextDouble() * 9.5), 2); // 0.5 - 10.0 µCi
            return (val, uCiUnit);
        }
        else if (iso.Symbol == "Ir-192" || iso.Symbol == "Se-75")
        {
            // مصادر التصوير الصناعي: قيم بالكوري أو الميليكوري
            if (random.Next(100) < 50)
            {
                double val = Math.Round(5.0 + (random.NextDouble() * 45.0), 1); // 5 - 50 Ci
                return (val, ciUnit);
            }
            else
            {
                double val = Math.Round(100.0 + (random.NextDouble() * 800.0), 0); // 100 - 900 mCi
                return (val, mCiUnit);
            }
        }
        else if (iso.Symbol == "Tc-99m" || iso.Symbol == "F-18" || iso.Symbol == "I-131" || iso.Symbol == "Lu-177")
        {
            // مصادر ونظائر طبية: عادة 5 إلى 250 mCi
            double val = Math.Round(5.0 + (random.NextDouble() * 245.0), 1);
            return (val, mCiUnit);
        }
        else if (iso.Symbol == "K-40")
        {
            // طبيعي منخفض الشدة: بالبكريل أو الميكروكوري
            double val = Math.Round(500.0 + (random.NextDouble() * 4500.0), 0); // 500 - 5000 Bq
            return (val, bqUnit);
        }
        else
        {
            // مصادر غاما وبيتا المعيارية والمختبرية (Co-60, Cs-137, Ba-133, Na-22, Eu-152)
            if (random.Next(100) < 60)
            {
                double val = Math.Round(1.0 + (random.NextDouble() * 50.0), 1); // 1 - 50 µCi
                return (val, uCiUnit);
            }
            else
            {
                double val = Math.Round(0.5 + (random.NextDouble() * 20.0), 2); // 0.5 - 20 mCi
                return (val, mCiUnit);
            }
        }
    }
}
#endif
