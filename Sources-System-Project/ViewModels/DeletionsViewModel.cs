using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.EntityFrameworkCore;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;

namespace Sources.ViewModels
{
    public class DeletedItemRow
    {
        public Guid Id { get; set; }
        public string EntityType { get; set; } = string.Empty; // "Source", "Location", "User", "Radioisotope"
        public string EntityTypeDisplayName { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public string SecondaryIdentifier { get; set; } = string.Empty;
        public string DeletedByName { get; set; } = "-";
        public DateTime? DeletedAt { get; set; }
        public string DeletedAtFormatted => DeletedAt.HasValue ? DeletedAt.Value.ToString("yyyy/MM/dd HH:mm") : "-";
        public object EntityObject { get; set; } = null!;
        public PackIconKind IconKind { get; set; }
        public string BadgeBackgroundHex { get; set; } = "#0284C7";
        public string BadgeForegroundHex { get; set; } = "#FFFFFF";
    }

    public partial class DeletionsViewModel : ObservableObject
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly ISourceService _sourceService;
        private readonly ILocationService _locationService;
        private readonly IUserService _userService;
        private readonly IRadioisotopeService _radioisotopeService;
        private readonly INeutronSourceService _neutronSourceService;

        [ObservableProperty] private ObservableCollection<DeletedItemRow> _allItems = new();
        [ObservableProperty] private ObservableCollection<DeletedItemRow> _filteredItems = new();
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedFilter = "All"; // "All", "Sources", "NeutronSources", "Locations", "Users", "Radioisotopes"
        [ObservableProperty] private bool _isLoading;

        // ─── إحصائيات المحذوفات (KPIs) ───
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _sourcesCount;
        [ObservableProperty] private int _neutronSourcesCount;
        [ObservableProperty] private int _locationsCount;
        [ObservableProperty] private int _usersCount;
        [ObservableProperty] private int _radioisotopesCount;

        public DeletionsViewModel(
            IDbContextFactory<AppDbContext> dbFactory,
            ISourceService? sourceService = null,
            ILocationService? locationService = null,
            IUserService? userService = null,
            IRadioisotopeService? radioisotopeService = null,
            INeutronSourceService? neutronSourceService = null)
        {
            _dbFactory = dbFactory;
            var defaultUserSvc = userService ?? new UserService(dbFactory);
            var defaultAuditSvc = new AuditService(dbFactory, defaultUserSvc);

            _userService = defaultUserSvc;
            _sourceService = sourceService ?? new SourceService(dbFactory, new DecayCalculationService(), defaultAuditSvc, defaultUserSvc);
            _locationService = locationService ?? new LocationService(dbFactory, defaultAuditSvc, defaultUserSvc);
            _radioisotopeService = radioisotopeService ?? new RadioisotopeService(dbFactory, defaultAuditSvc, defaultUserSvc);
            _neutronSourceService = neutronSourceService ?? new NeutronSourceService(dbFactory, defaultAuditSvc, defaultUserSvc);

            _ = LoadDeletedItemsAsync();
        }

        public async Task LoadDeletedItemsAsync()
        {
            IsLoading = true;
            try
            {
                var items = await Task.Run(() =>
                {
                    var result = new List<DeletedItemRow>();
                    using var db = _dbFactory.CreateDbContext();

                    // 1. المصادر المحذوفة (Sources)
                    var deletedSources = db.Sources
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Include(s => s.Radioisotope)
                        .Include(s => s.InitialActivityUnit)
                        .Include(s => s.CurrentActivityUnit)
                        .Include(s => s.Location)
                        .Include(s => s.DeletedByUser)
                        .Include(s => s.SourceIsotopes).ThenInclude(si => si.Radioisotope)
                        .Where(s => s.IsDeleted)
                        .ToList();

                    foreach (var s in deletedSources)
                    {
                        var isotopeName = s.Radioisotope?.Symbol ?? (s.SourceIsotopes?.FirstOrDefault()?.Radioisotope?.Symbol ?? "");
                        result.Add(new DeletedItemRow
                        {
                            Id = s.Id,
                            EntityType = "Source",
                            EntityTypeDisplayName = TranslationHelper.GetString("EntityTypeSource") ?? "مصدر مشع",
                            Identifier = s.SourceCode,
                            SecondaryIdentifier = !string.IsNullOrEmpty(isotopeName) ? $"({isotopeName})" : string.Empty,
                            DeletedByName = s.DeletedByUser?.FullName ?? "-",
                            DeletedAt = s.DeletedAt ?? s.CreatedAt,
                            EntityObject = s,
                            IconKind = PackIconKind.Radioactive,
                            BadgeBackgroundHex = "#0284C7",
                            BadgeForegroundHex = "#FFFFFF"
                        });
                    }

                    // 2. المواقع المحذوفة (Locations)
                    var deletedLocations = db.Locations
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Include(l => l.DeletedByUser)
                        .Where(l => l.IsDeleted)
                        .ToList();

                    foreach (var l in deletedLocations)
                    {
                        result.Add(new DeletedItemRow
                        {
                            Id = l.Id,
                            EntityType = "Location",
                            EntityTypeDisplayName = TranslationHelper.GetString("EntityTypeLocation") ?? "موقع",
                            Identifier = l.LocationName,
                            SecondaryIdentifier = !string.IsNullOrEmpty(l.Building) ? TranslationHelper.GetFormat("TextBuildingLabelFormat", l.Building) : string.Empty,
                            DeletedByName = l.DeletedByUser?.FullName ?? "-",
                            DeletedAt = l.DeletedAt,
                            EntityObject = l,
                            IconKind = PackIconKind.MapMarkerOutline,
                            BadgeBackgroundHex = "#10B981",
                            BadgeForegroundHex = "#FFFFFF"
                        });
                    }

                    // 3. المستخدمين المحذوفين (Users)
                    var deletedUsers = db.Users
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Include(u => u.Role)
                        .Include(u => u.DeletedByUser)
                        .Where(u => u.IsDeleted)
                        .ToList();

                    foreach (var u in deletedUsers)
                    {
                        result.Add(new DeletedItemRow
                        {
                            Id = u.Id,
                            EntityType = "User",
                            EntityTypeDisplayName = TranslationHelper.GetString("EntityTypeUser") ?? "مستخدم",
                            Identifier = u.FullName,
                            SecondaryIdentifier = $"(@{u.Username})",
                            DeletedByName = u.DeletedByUser?.FullName ?? "-",
                            DeletedAt = u.DeletedAt,
                            EntityObject = u,
                            IconKind = PackIconKind.AccountOutline,
                            BadgeBackgroundHex = "#8B5CF6",
                            BadgeForegroundHex = "#FFFFFF"
                        });
                    }

                    // 4. النظائر المحذوفة (Radioisotopes)
                    var deletedIsotopes = db.Radioisotopes
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Include(r => r.DeletedByUser)
                        .Where(r => r.IsDeleted)
                        .ToList();

                    foreach (var r in deletedIsotopes)
                    {
                        result.Add(new DeletedItemRow
                        {
                            Id = r.Id,
                            EntityType = "Radioisotope",
                            EntityTypeDisplayName = TranslationHelper.GetString("EntityTypeRadioisotope") ?? "نظير مشع",
                            Identifier = r.Symbol,
                            SecondaryIdentifier = !string.IsNullOrEmpty(r.DisplayName) && r.DisplayName != r.Symbol ? $"({r.DisplayName})" : string.Empty,
                            DeletedByName = r.DeletedByUser?.FullName ?? "-",
                            DeletedAt = r.DeletedAt,
                            EntityObject = r,
                            IconKind = PackIconKind.Atom,
                            BadgeBackgroundHex = "#F59E0B",
                            BadgeForegroundHex = "#FFFFFF"
                        });
                    }

                    // 5. المصادر النيترونية المحذوفة (NeutronSources)
                    var deletedNeutronSources = db.NeutronSources
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Include(n => n.NeutronSourceType)
                        .Include(n => n.Location)
                        .Include(n => n.DeletedByUser)
                        .Where(n => n.IsDeleted)
                        .ToList();

                    foreach (var n in deletedNeutronSources)
                    {
                        var typeCode = n.NeutronSourceType?.Code ?? "";
                        result.Add(new DeletedItemRow
                        {
                            Id = n.Id,
                            EntityType = "NeutronSource",
                            EntityTypeDisplayName = TranslationHelper.GetString("EntityTypeNeutronSource") ?? "مصدر نيتروني",
                            Identifier = n.SourceCode,
                            SecondaryIdentifier = !string.IsNullOrEmpty(typeCode) ? $"({typeCode})" : string.Empty,
                            DeletedByName = n.DeletedByUser?.FullName ?? "-",
                            DeletedAt = n.DeletedAt ?? n.CreatedAt,
                            EntityObject = n,
                            IconKind = PackIconKind.AtomVariant,
                            BadgeBackgroundHex = "#0D9488",
                            BadgeForegroundHex = "#FFFFFF"
                        });
                    }

                    // ترتيب زمني تنازلي حسب تاريخ الحذف
                    return result.OrderByDescending(r => r.DeletedAt ?? DateTime.MinValue)
                                 .ThenBy(r => r.Identifier)
                                 .ToList();
                });

                AllItems = new ObservableCollection<DeletedItemRow>(items);

                // تحديث العدادات
                SourcesCount = AllItems.Count(i => i.EntityType == "Source");
                NeutronSourcesCount = AllItems.Count(i => i.EntityType == "NeutronSource");
                LocationsCount = AllItems.Count(i => i.EntityType == "Location");
                UsersCount = AllItems.Count(i => i.EntityType == "User");
                RadioisotopesCount = AllItems.Count(i => i.EntityType == "Radioisotope");
                TotalCount = AllItems.Count;

                ApplyFilter();
            }
            catch (Exception ex)
            {
                LoggerService.LogError("DeletionsViewModel: Failed to load deleted items", ex);
                DialogHelper.ShowError(TranslationHelper.GetFormat("MsgErrLoadDeletionsLog", ex.Message), TranslationHelper.GetString("TitleDeletionsLog") ?? "سجل المحذوفات");
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        [RelayCommand]
        public void SetFilter(string filter)
        {
            SelectedFilter = filter;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var query = AllItems.AsEnumerable();

            // 1. التصفية حسب النوع
            if (SelectedFilter != "All")
            {
                query = SelectedFilter switch
                {
                    "Sources" => query.Where(i => i.EntityType == "Source"),
                    "NeutronSources" => query.Where(i => i.EntityType == "NeutronSource"),
                    "Locations" => query.Where(i => i.EntityType == "Location"),
                    "Users" => query.Where(i => i.EntityType == "User"),
                    "Radioisotopes" => query.Where(i => i.EntityType == "Radioisotope"),
                    _ => query
                };
            }

            // 2. التصفية بالبحث النصي
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var text = SearchText.Trim();
                query = query.Where(i =>
                    i.Identifier.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    i.SecondaryIdentifier.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    i.EntityTypeDisplayName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    i.DeletedByName.Contains(text, StringComparison.OrdinalIgnoreCase));
            }

            FilteredItems = new ObservableCollection<DeletedItemRow>(query.ToList());
        }

        [RelayCommand]
        public void Refresh()
        {
            _ = LoadDeletedItemsAsync();
        }

        [RelayCommand]
        public void ViewDetails(DeletedItemRow? row)
        {
            if (row == null || row.EntityObject == null) return;

            switch (row.EntityType)
            {
                case "Source" when row.EntityObject is Source src:
                    SourceNavigationHelper.OpenSourceDetails(src);
                    break;

                case "NeutronSource" when row.EntityObject is NeutronSource ns:
                    ShowNeutronSourceDetailsDialog(ns, row);
                    break;

                case "Location" when row.EntityObject is Location loc:
                    ShowLocationDetailsDialog(loc, row);
                    break;

                case "User" when row.EntityObject is User user:
                    ShowUserDetailsDialog(user, row);
                    break;

                case "Radioisotope" when row.EntityObject is Radioisotope iso:
                    ShowRadioisotopeDetailsDialog(iso, row);
                    break;

                default:
                    DialogHelper.ShowInfo(TranslationHelper.GetFormat("MsgDeletedItemDetailsFormat", row.Identifier, row.EntityTypeDisplayName, row.DeletedByName, row.DeletedAtFormatted), TranslationHelper.GetString("TitleDeletedItemDetails") ?? "تفاصيل العنصر المحذوف");
                    break;
            }
        }

        [RelayCommand]
        public async Task RestoreItem(DeletedItemRow? row)
        {
            if (row == null) return;

            string confirmPrompt = TranslationHelper.GetFormat("MsgConfirmRestoreItemFormat", row.EntityTypeDisplayName, row.Identifier, row.SecondaryIdentifier);
            bool confirmed = DialogHelper.ShowConfirmation(confirmPrompt, TranslationHelper.GetString("TitleConfirmRestoreItem") ?? "تأكيد استرجاع العنصر");
            if (!confirmed) return;

            (bool Success, string Message) result = (false, string.Empty);

            switch (row.EntityType)
            {
                case "Source":
                    result = _sourceService.RestoreSource(row.Id);
                    break;
                case "NeutronSource":
                    result = _neutronSourceService.Restore(row.Id);
                    break;
                case "Location":
                    result = _locationService.Restore(row.Id);
                    break;
                case "User":
                    result = _userService.RestoreUser(row.Id);
                    break;
                case "Radioisotope":
                    result = _radioisotopeService.Restore(row.Id);
                    break;
                default:
                    result = (false, TranslationHelper.GetString("MsgErrUnknownEntityType") ?? "نوع الكيان غير معروف");
                    break;
            }

            if (result.Success)
            {
                DialogHelper.ShowInfo(result.Message, TranslationHelper.GetString("TitleRestoreSuccess") ?? "تم الاسترجاع بنجاح");
                await LoadDeletedItemsAsync();
            }
            else
            {
                DialogHelper.ShowWarning(result.Message, TranslationHelper.GetString("TitleRestoreFailed") ?? "تعذر الاسترجاع");
            }
        }

        private void ShowNeutronSourceDetailsDialog(NeutronSource ns, DeletedItemRow row)
        {
            string info = TranslationHelper.GetFormat("MsgNeutronSourceDeletedDetailsFormat",
                ns.SourceCode,
                ns.NeutronSourceType?.Code ?? "-",
                ns.NeutronSourceType?.NameAr ?? ns.NeutronSourceType?.NameEn ?? "-",
                ns.SerialNumber ?? "-",
                ns.CalibratedEmissionRateFormatted,
                ns.RelativeExpandedUncertaintyPercent.HasValue ? $"{ns.RelativeExpandedUncertaintyPercent:N1}%" : "-",
                ns.Location?.LocationName ?? "-",
                ns.CalibrationDate.HasValue ? ns.CalibrationDate.Value.ToString("yyyy/MM/dd") : string.Empty,
                row.DeletedAtFormatted,
                row.DeletedByName);

            DialogHelper.ShowInfo(info, TranslationHelper.GetFormat("TitleDeletedNeutronSourceDetailsFormat", ns.SourceCode));
        }

        // اسم بديل للأمر للتوافق
        [RelayCommand]
        public Task Restore(DeletedItemRow? row) => RestoreItem(row);

        private void ShowLocationDetailsDialog(Location loc, DeletedItemRow row)
        {
            string info = TranslationHelper.GetFormat("MsgLocationDeletedDetailsFormat",
                loc.LocationName,
                loc.LocationType ?? "-",
                loc.Building ?? "-",
                loc.Room ?? "-",
                loc.ResponsiblePerson ?? "-",
                row.DeletedAtFormatted,
                row.DeletedByName);

            DialogHelper.ShowInfo(info, TranslationHelper.GetFormat("TitleDeletedLocationDetailsFormat", loc.LocationName));
        }

        private void ShowUserDetailsDialog(User user, DeletedItemRow row)
        {
            string info = TranslationHelper.GetFormat("MsgUserDeletedDetailsFormat",
                user.FullName,
                user.Username,
                user.Role?.RoleName ?? "-",
                user.Email ?? "-",
                user.CreatedAt.ToString("yyyy/MM/dd"),
                row.DeletedAtFormatted,
                row.DeletedByName);

            DialogHelper.ShowInfo(info, TranslationHelper.GetFormat("TitleDeletedUserDetailsFormat", user.FullName));
        }

        private void ShowRadioisotopeDetailsDialog(Radioisotope iso, DeletedItemRow row)
        {
            string info = TranslationHelper.GetFormat("MsgRadioisotopeDeletedDetailsFormat",
                iso.Symbol,
                iso.DisplayName,
                iso.RadiationType,
                iso.DisplayHalfLife,
                iso.Energy,
                iso.Category,
                iso.GammaConstant.HasValue ? iso.GammaConstant.Value.ToString("0.####") : "-",
                row.DeletedAtFormatted,
                row.DeletedByName,
                string.IsNullOrEmpty(iso.DisplayNotes) ? "-" : iso.DisplayNotes);

            DialogHelper.ShowInfo(info, TranslationHelper.GetFormat("TitleDeletedRadioisotopeDetailsFormat", iso.Symbol));
        }
    }
}
