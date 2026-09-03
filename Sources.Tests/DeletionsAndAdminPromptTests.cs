using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Sources.Data;
using Sources.Helpers;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Sources.ViewModels;
using Sources.Views;
using Xunit;

namespace Sources.Tests
{
    public class DeletionsAndAdminPromptTests : IClassFixture<SqliteInMemoryFixture>, IDisposable
    {
        private readonly SqliteInMemoryFixture _fixture;

        public DeletionsAndAdminPromptTests(SqliteInMemoryFixture fixture)
        {
            _fixture = fixture;
            _fixture.ResetDatabase();
            DialogHelper.IsTestMode = true;
            DialogHelper.ShowConfirmationResult = null;
            PasswordPromptDialog.CustomPromptResult = null;
        }

        public void Dispose()
        {
            _fixture.ResetDatabase();
            DialogHelper.IsTestMode = false;
            DialogHelper.ShowConfirmationResult = null;
            DialogHelper.LastMessage = null;
            DialogHelper.LastTitle = null;
            PasswordPromptDialog.CustomPromptResult = null;
        }

        #region 1. Password Prompt Admin Validation Tests

        [Fact]
        public void ValidateAdminPassword_ReturnsSuccess_WhenAdminEntersCorrectPassword()
        {
            // Arrange
            var adminRole = new Role { RoleName = "مدير النظام" };
            var adminUser = new User
            {
                Username = "admin",
                FullName = "مدير المنظومة",
                Role = adminRole,
                PasswordHash = PasswordHelper.HashPassword("AdminSecret123")
            };

            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(u => u.CurrentUser).Returns(adminUser);

            // Act
            var (success, message) = PasswordPromptDialog.ValidateAdminPassword(mockUserService.Object, "AdminSecret123");

            // Assert
            Assert.True(success);
            Assert.Equal("تم التحقق بنجاح", message);
        }

        [Fact]
        public void ValidateAdminPassword_ReturnsFalse_WhenAdminEntersWrongPassword()
        {
            // Arrange
            var adminRole = new Role { RoleName = "مدير النظام" };
            var adminUser = new User
            {
                Username = "admin",
                FullName = "مدير المنظومة",
                Role = adminRole,
                PasswordHash = PasswordHelper.HashPassword("AdminSecret123")
            };

            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(u => u.CurrentUser).Returns(adminUser);

            // Act
            var (success, message) = PasswordPromptDialog.ValidateAdminPassword(mockUserService.Object, "WrongPassword999");

            // Assert
            Assert.False(success);
            Assert.True(message == "MsgErrIncorrectAdminPassword" || message.Contains("كلمة المرور غير صحيحة"));
        }

        [Fact]
        public void ValidateAdminPassword_ReturnsFalse_WhenUserIsNotAdmin()
        {
            // Arrange
            var operatorRole = new Role { RoleName = "مشغل أجهزة" };
            var operatorUser = new User
            {
                Username = "operator1",
                FullName = "مشغل النظام",
                Role = operatorRole,
                PasswordHash = PasswordHelper.HashPassword("OperatorPass123")
            };

            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(u => u.CurrentUser).Returns(operatorUser);

            // Act
            var (success, message) = PasswordPromptDialog.ValidateAdminPassword(mockUserService.Object, "OperatorPass123");

            // Assert
            Assert.False(success);
            Assert.True(message == "MsgErrAdminOnly" || message.Contains("غير مصرح: هذه العملية مخصصة لمدير النظام فقط"));
        }

        [Fact]
        public void ValidateAdminPassword_ReturnsFalse_WhenCurrentUserIsNull()
        {
            // Arrange
            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(u => u.CurrentUser).Returns((User?)null);

            // Act
            var (success, message) = PasswordPromptDialog.ValidateAdminPassword(mockUserService.Object, "AnyPass");

            // Assert
            Assert.False(success);
            Assert.True(message == "MsgErrNoCurrentUser" || message.Contains("لا يوجد مستخدم مسجل حالياً في الجلسة"));
        }

        [Fact]
        public void RequestAdminAccess_HonorsCustomPromptResult()
        {
            // Test true override
            PasswordPromptDialog.CustomPromptResult = true;
            Assert.True(PasswordPromptDialog.RequestAdminAccess());

            // Test false override
            PasswordPromptDialog.CustomPromptResult = false;
            Assert.False(PasswordPromptDialog.RequestAdminAccess());
        }

        #endregion

        #region 2. DeletionsViewModel Aggregation & Filtering Tests

        [Fact]
        public async Task DeletionsViewModel_AggregatesDeletedItems_FromAllFourEntities()
        {
            // Arrange
            var testRole = new Role { Id = Guid.NewGuid(), RoleName = "مدير النظام" };

            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                FullName = "مدير المراجعة",
                Username = "audit_admin",
                PasswordHash = "hash",
                RoleId = testRole.Id,
                Role = testRole
            };

            var deletedUser = new User
            {
                Id = Guid.NewGuid(),
                FullName = "الموظف السابق",
                Username = "former_staff",
                PasswordHash = "hash",
                RoleId = testRole.Id,
                Role = testRole,
                IsDeleted = true,
                DeletedAt = new DateTime(2026, 8, 10, 10, 0, 0),
                DeletedBy = adminUser.Id,
                DeletedByUser = adminUser
            };

            var activeUser = new User
            {
                Id = Guid.NewGuid(),
                FullName = "الموظف الحالي",
                Username = "current_staff",
                PasswordHash = "hash",
                RoleId = testRole.Id,
                Role = testRole,
                IsDeleted = false
            };

            var testUnit = new ActivityUnit { UnitName = "Megabecquerel", UnitSymbol = "MBq", ConversionToBq = 1e6 };

            var activeIsotope = new Radioisotope
            {
                Id = Guid.NewGuid(),
                Symbol = "Cs-137",
                Name = "Cesium-137",
                ArabicName = "سيزيوم-137",
                HalfLife = 30.08,
                IsDeleted = false
            };

            var deletedIsotope = new Radioisotope
            {
                Id = Guid.NewGuid(),
                Symbol = "Co-60",
                Name = "Cobalt-60",
                ArabicName = "كوبالت-60",
                HalfLife = 5.27,
                IsDeleted = true,
                DeletedAt = new DateTime(2026, 8, 12, 14, 0, 0),
                DeletedBy = adminUser.Id,
                DeletedByUser = adminUser
            };

            var activeLocation = new Location
            {
                Id = Guid.NewGuid(),
                LocationName = "المختبر المركزي A",
                Building = "1",
                IsDeleted = false
            };

            var deletedLocation = new Location
            {
                Id = Guid.NewGuid(),
                LocationName = "المخزن القديم B",
                Building = "4",
                IsDeleted = true,
                DeletedAt = new DateTime(2026, 8, 15, 9, 30, 0),
                DeletedBy = adminUser.Id,
                DeletedByUser = adminUser
            };

            var activeSource = new Source
            {
                Id = Guid.NewGuid(),
                SourceCode = "SRC-ACTIVE-01",
                RadioisotopeId = activeIsotope.Id,
                InitialActivityUnitId = testUnit.Id,
                CurrentActivityUnitId = testUnit.Id,
                InitialActivityValue = 100,
                CurrentActivityValue = 90,
                CalibrationDate = DateTime.Now.AddYears(-1),
                LocationId = activeLocation.Id,
                IsDeleted = false
            };

            var deletedSource1 = new Source
            {
                Id = Guid.NewGuid(),
                SourceCode = "SRC-DEL-01",
                RadioisotopeId = activeIsotope.Id,
                InitialActivityUnitId = testUnit.Id,
                CurrentActivityUnitId = testUnit.Id,
                InitialActivityValue = 50,
                CurrentActivityValue = 40,
                CalibrationDate = DateTime.Now.AddYears(-2),
                IsDeleted = true,
                DeletedAt = new DateTime(2026, 8, 20, 11, 0, 0),
                DeletedBy = adminUser.Id,
                DeletedByUser = adminUser
            };

            var deletedSource2 = new Source
            {
                Id = Guid.NewGuid(),
                SourceCode = "SRC-DEL-02",
                RadioisotopeId = deletedIsotope.Id,
                InitialActivityUnitId = testUnit.Id,
                CurrentActivityUnitId = testUnit.Id,
                InitialActivityValue = 200,
                CurrentActivityValue = 150,
                CalibrationDate = DateTime.Now.AddYears(-3),
                IsDeleted = true,
                DeletedAt = new DateTime(2026, 8, 22, 16, 45, 0),
                DeletedBy = adminUser.Id,
                DeletedByUser = adminUser
            };

            using (var db = _fixture.CreateContext())
            {
                db.Roles.Add(testRole);
                db.Users.AddRange(adminUser, deletedUser, activeUser);
                db.ActivityUnits.Add(testUnit);
                db.Radioisotopes.AddRange(activeIsotope, deletedIsotope);
                db.Locations.AddRange(activeLocation, deletedLocation);
                db.Sources.AddRange(activeSource, deletedSource1, deletedSource2);
                db.SaveChanges();
            }

            // Act: Instantiate DeletionsViewModel and load
            var vm = new DeletionsViewModel(_fixture.ContextFactory);
            await vm.LoadDeletedItemsAsync();

            // Assert: Total deleted = 2 Sources + 1 Location + 1 User + 1 Radioisotope = 5 items
            Assert.Equal(5, vm.TotalCount);
            Assert.Equal(2, vm.SourcesCount);
            Assert.Equal(1, vm.LocationsCount);
            Assert.Equal(1, vm.UsersCount);
            Assert.Equal(1, vm.RadioisotopesCount);
            Assert.Equal(5, vm.FilteredItems.Count);

            // Verify sorting: Most recent DeletedAt is first (SRC-DEL-02 at 2026-08-22)
            Assert.Equal("SRC-DEL-02", vm.FilteredItems[0].Identifier);
            Assert.Equal("Source", vm.FilteredItems[0].EntityType);

            // Verify deleted by name is populated
            Assert.All(vm.FilteredItems, item => Assert.Equal("مدير المراجعة", item.DeletedByName));
        }

        [Fact]
        public async Task DeletionsViewModel_FilteringByType_WorksCorrectly()
        {
            // Arrange
            var testRole = new Role { Id = Guid.NewGuid(), RoleName = "مستخدم" };
            var testUnit = new ActivityUnit { UnitName = "Curie", UnitSymbol = "Ci", ConversionToBq = 3.7e10 };
            var isotope = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Am-241", Name = "Americium-241", IsDeleted = true, DeletedAt = DateTime.Now };
            var location = new Location { Id = Guid.NewGuid(), LocationName = "موقع محذوف", IsDeleted = true, DeletedAt = DateTime.Now };
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = "مستخدم محذوف",
                Username = "del_u",
                PasswordHash = "p",
                RoleId = testRole.Id,
                Role = testRole,
                IsDeleted = true,
                DeletedAt = DateTime.Now
            };
            var source = new Source
            {
                Id = Guid.NewGuid(),
                SourceCode = "SRC-TEST-DEL",
                RadioisotopeId = isotope.Id,
                InitialActivityUnitId = testUnit.Id,
                CurrentActivityUnitId = testUnit.Id,
                IsDeleted = true,
                DeletedAt = DateTime.Now
            };

            using (var db = _fixture.CreateContext())
            {
                db.Roles.Add(testRole);
                db.ActivityUnits.Add(testUnit);
                db.Radioisotopes.Add(isotope);
                db.Locations.Add(location);
                db.Users.Add(user);
                db.Sources.Add(source);
                db.SaveChanges();
            }

            var vm = new DeletionsViewModel(_fixture.ContextFactory);
            await vm.LoadDeletedItemsAsync();

            // Act & Assert: Filter Sources
            vm.SetFilter("Sources");
            Assert.Single(vm.FilteredItems);
            Assert.Equal("Source", vm.FilteredItems[0].EntityType);
            Assert.Equal("SRC-TEST-DEL", vm.FilteredItems[0].Identifier);

            // Filter Locations
            vm.SetFilter("Locations");
            Assert.Single(vm.FilteredItems);
            Assert.Equal("Location", vm.FilteredItems[0].EntityType);
            Assert.Equal("موقع محذوف", vm.FilteredItems[0].Identifier);

            // Filter Users
            vm.SetFilter("Users");
            Assert.Single(vm.FilteredItems);
            Assert.Equal("User", vm.FilteredItems[0].EntityType);
            Assert.Equal("مستخدم محذوف", vm.FilteredItems[0].Identifier);

            // Filter Radioisotopes
            vm.SetFilter("Radioisotopes");
            Assert.Single(vm.FilteredItems);
            Assert.Equal("Radioisotope", vm.FilteredItems[0].EntityType);
            Assert.Equal("Am-241", vm.FilteredItems[0].Identifier);

            // Filter All
            vm.SetFilter("All");
            Assert.Equal(4, vm.FilteredItems.Count);
        }

        [Fact]
        public async Task DeletionsViewModel_SearchText_FiltersMatchingItems()
        {
            // Arrange
            var testUnit = new ActivityUnit { UnitName = "Becquerel", UnitSymbol = "Bq", ConversionToBq = 1 };
            var iso1 = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Ba-133", Name = "Barium-133", ArabicName = "باريوم", IsDeleted = true, DeletedAt = DateTime.Now };
            var iso2 = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Co-57", Name = "Cobalt-57", ArabicName = "كوبالت", IsDeleted = true, DeletedAt = DateTime.Now };

            using (var db = _fixture.CreateContext())
            {
                db.ActivityUnits.Add(testUnit);
                db.Radioisotopes.AddRange(iso1, iso2);
                db.SaveChanges();
            }

            var vm = new DeletionsViewModel(_fixture.ContextFactory);
            await vm.LoadDeletedItemsAsync();

            // Act: Search for Ba-133
            vm.SearchText = "Ba-133";

            // Assert
            Assert.Single(vm.FilteredItems);
            Assert.Equal("Ba-133", vm.FilteredItems[0].Identifier);

            // Clear search
            vm.SearchText = string.Empty;
            Assert.Equal(2, vm.FilteredItems.Count);
        }

        [Fact]
        public async Task DeletionsViewModel_ViewDetails_ExecutesWithoutError()
        {
            // Arrange
            var testRole = new Role { Id = Guid.NewGuid(), RoleName = "مستخدم" };
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = "علي سالم",
                Username = "ali",
                PasswordHash = "h",
                RoleId = testRole.Id,
                Role = testRole,
                IsDeleted = true,
                DeletedAt = DateTime.Now
            };
            var loc = new Location { Id = Guid.NewGuid(), LocationName = "مستودع 5", Building = "B", IsDeleted = true, DeletedAt = DateTime.Now };
            var iso = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Ir-192", Name = "Iridium-192", IsDeleted = true, DeletedAt = DateTime.Now };

            using (var db = _fixture.CreateContext())
            {
                db.Roles.Add(testRole);
                db.Users.Add(user);
                db.Locations.Add(loc);
                db.Radioisotopes.Add(iso);
                db.SaveChanges();
            }

            var vm = new DeletionsViewModel(_fixture.ContextFactory);
            await vm.LoadDeletedItemsAsync();

            // Act & Assert: Call ViewDetails for each row
            foreach (var row in vm.AllItems)
            {
                vm.ViewDetails(row);
                Assert.NotNull(DialogHelper.LastMessage);
            }
        }

        #endregion

        #region 3. MainViewModel Deletions Navigation Protection Tests

        [Fact]
        public void MainViewModel_NavigateToDeletions_GrantedWhenAdminPromptSucceeds()
        {
            // Arrange
            var mockUserService = new Mock<IUserService>();
            var adminUser = new User
            {
                FullName = "المدير العام",
                Username = "admin",
                Role = new Role { RoleName = "مدير النظام" }
            };
            mockUserService.Setup(u => u.CurrentUser).Returns(adminUser);
            mockUserService.Setup(u => u.IsLoggedIn).Returns(true);

            var mockAlertService = new Mock<IAlertService>();
            var mockSettingsService = new Mock<ISystemSettingsService>();

            var vm = new MainViewModel(mockUserService.Object, mockAlertService.Object, mockSettingsService.Object);

            PasswordPromptDialog.CustomPromptResult = true;

            // Act
            vm.NavigateTo("Deletions");

            // Assert
            Assert.Equal("Deletions", vm.CurrentViewName);
        }

        [Fact]
        public void MainViewModel_NavigateToDeletions_BlockedWhenAdminPromptFails()
        {
            // Arrange
            var mockUserService = new Mock<IUserService>();
            var adminUser = new User
            {
                FullName = "المدير العام",
                Username = "admin",
                Role = new Role { RoleName = "مدير النظام" }
            };
            mockUserService.Setup(u => u.CurrentUser).Returns(adminUser);
            mockUserService.Setup(u => u.IsLoggedIn).Returns(true);

            var mockAlertService = new Mock<IAlertService>();
            var mockSettingsService = new Mock<ISystemSettingsService>();

            var vm = new MainViewModel(mockUserService.Object, mockAlertService.Object, mockSettingsService.Object);
            vm.NavigateTo("Dashboard");
            Assert.Equal("Dashboard", vm.CurrentViewName);

            // Act: Reject password prompt
            PasswordPromptDialog.CustomPromptResult = false;
            vm.NavigateTo("Deletions");

            // Assert: View should remain "Dashboard"
            Assert.Equal("Dashboard", vm.CurrentViewName);
        }

        #endregion

        #region 4. Round 82 Restoration Logic & Integrity Tests

        [Fact]
        public void RestoreSource_Succeeds_AndClearsDeletionFlags_AndLogsAudit()
        {
            // Arrange
            var location = new Location { Id = Guid.NewGuid(), LocationName = "مختبر غاما 1", IsDeleted = false };
            var isotope = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Cs-137", Name = "Cesium-137", HalfLife = 30.17, RadiationType = "Gamma" };
            var unit = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "MBq", UnitSymbol = "MBq", ConversionToBq = 1e6 };
            var adminUser = new User { Id = Guid.NewGuid(), Username = "admin_user", FullName = "مدير التدقيق", Role = new Role { RoleName = "مدير النظام", Permissions = "All" }, Permissions = "All", IsEditor = true };

            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                db.Locations.Add(location);
                db.Radioisotopes.Add(isotope);
                db.ActivityUnits.Add(unit);
                db.Users.Add(adminUser);

                var deletedSource = new Source
                {
                    Id = Guid.NewGuid(),
                    SourceCode = "SRC-RESTORE-01",
                    LocationId = location.Id,
                    RadioisotopeId = isotope.Id,
                    InitialActivityValue = 100,
                    InitialActivityUnitId = unit.Id,
                    CalibrationDate = DateTime.Now.AddYears(-1),
                    CurrentActivityValue = 95,
                    CurrentActivityUnitId = unit.Id,
                    Status = "InUse",
                    IsDeleted = true,
                    DeletedAt = DateTime.Now.AddDays(-2),
                    DeletedBy = adminUser.Id
                };
                db.Sources.Add(deletedSource);
                db.SaveChanges();
            }

            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(u => u.CurrentUser).Returns(adminUser);
            var auditService = new AuditService(_fixture.ContextFactory, mockUserService.Object);
            var sourceService = new SourceService(_fixture.ContextFactory, new DecayCalculationService(), auditService, mockUserService.Object);

            var deletedSourceId = Guid.Empty;
            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                deletedSourceId = db.Sources.IgnoreQueryFilters().First(s => s.SourceCode == "SRC-RESTORE-01").Id;
            }

            // Act
            var (success, message) = sourceService.RestoreSource(deletedSourceId);

            // Assert
            Assert.True(success);
            Assert.Contains("SRC-RESTORE-01", message);
            Assert.Contains("مختبر غاما 1", message);

            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                var restoredSource = db.Sources.FirstOrDefault(s => s.Id == deletedSourceId);
                Assert.NotNull(restoredSource);
                Assert.False(restoredSource.IsDeleted);
                Assert.Null(restoredSource.DeletedAt);
                Assert.Null(restoredSource.DeletedBy);

                // Verify AuditLog
                var log = db.AuditLogs.FirstOrDefault(a => a.TableName == "Sources" && a.RecordId == deletedSourceId && a.Action == "Restore");
                Assert.NotNull(log);
                Assert.Contains("SRC-RESTORE-01", log.Details);
            }
        }

        [Fact]
        public void RestoreLocation_Succeeds_AndClearsDeletionFlags_AndLogsAudit()
        {
            // Arrange
            var adminUser = new User { Id = Guid.NewGuid(), Username = "admin_loc", FullName = "مسؤول المواقع", Role = new Role { RoleName = "مدير النظام", Permissions = "All" }, Permissions = "All", IsEditor = true };
            var locationId = Guid.NewGuid();

            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                db.Users.Add(adminUser);
                db.Locations.Add(new Location
                {
                    Id = locationId,
                    LocationName = "مستودع أجهزة النظائر",
                    Building = "المبنى 3",
                    IsDeleted = true,
                    DeletedAt = DateTime.Now.AddDays(-1),
                    DeletedBy = adminUser.Id
                });
                db.SaveChanges();
            }

            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(u => u.CurrentUser).Returns(adminUser);
            var auditService = new AuditService(_fixture.ContextFactory, mockUserService.Object);
            var locationService = new LocationService(_fixture.ContextFactory, auditService, mockUserService.Object);

            // Act
            var (success, message) = locationService.Restore(locationId);

            // Assert
            Assert.True(success);
            Assert.Contains("مستودع أجهزة النظائر", message);

            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                var restored = db.Locations.FirstOrDefault(l => l.Id == locationId);
                Assert.NotNull(restored);
                Assert.False(restored.IsDeleted);
                Assert.Null(restored.DeletedAt);
                Assert.Null(restored.DeletedBy);

                // Verify AuditLog
                var log = db.AuditLogs.FirstOrDefault(a => a.TableName == "Locations" && a.RecordId == locationId && a.Action == "Restore");
                Assert.NotNull(log);
                Assert.Contains("مستودع أجهزة النظائر", log.Details);
            }
        }

        [Fact]
        public void RestoreUser_Succeeds_AndClearsDeletionFlags_AndLogsAudit()
        {
            // Arrange
            var role = new Role { Id = Guid.NewGuid(), RoleName = "مشغل" };
            var userId = Guid.NewGuid();
            var adminRole = new Role { Id = Guid.NewGuid(), RoleName = "مدير النظام", Permissions = "All" };
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin_usr",
                FullName = "مدير الحسابات",
                RoleId = adminRole.Id,
                Role = adminRole,
                PasswordHash = PasswordHelper.HashPassword("AdminPass123!"),
                Permissions = "All",
                IsActive = true,
                IsEditor = true
            };

            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                db.Roles.AddRange(role, adminRole);
                db.Users.Add(adminUser);
                db.Users.Add(new User
                {
                    Id = userId,
                    FullName = "أحمد خالد المشغل",
                    Username = "ahmed_khalid",
                    RoleId = role.Id,
                    IsActive = false,
                    IsDeleted = true,
                    DeletedAt = DateTime.Now.AddDays(-5),
                    DeletedBy = adminUser.Id
                });
                db.SaveChanges();
            }

            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(u => u.CurrentUser).Returns(adminUser);
            var auditService = new AuditService(_fixture.ContextFactory, mockUserService.Object);
            var userService = new UserService(_fixture.ContextFactory, auditService);
            userService.Login("admin_usr", "AdminPass123!");

            // Act
            var (success, message) = userService.RestoreUser(userId);

            // Assert
            Assert.True(success);
            Assert.Contains("أحمد خالد المشغل", message);

            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                var restored = db.Users.FirstOrDefault(u => u.Id == userId);
                Assert.NotNull(restored);
                Assert.False(restored.IsDeleted);
                Assert.True(restored.IsActive);
                Assert.Null(restored.DeletedAt);
                Assert.Null(restored.DeletedBy);

                // Verify AuditLog
                var log = db.AuditLogs.FirstOrDefault(a => a.TableName == "Users" && a.RecordId == userId && a.Action == "Restore");
                Assert.NotNull(log);
                Assert.Contains("أحمد خالد المشغل", log.Details);
            }
        }

        [Fact]
        public void RestoreRadioisotope_Succeeds_AndClearsDeletionFlags_AndLogsAudit()
        {
            // Arrange
            var isotopeId = Guid.NewGuid();
            var adminUser = new User { Id = Guid.NewGuid(), Username = "admin_iso", FullName = "مسؤول النظائر", Role = new Role { RoleName = "مدير النظام", Permissions = "All" }, Permissions = "All", IsEditor = true };

            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                db.Users.Add(adminUser);
                db.Radioisotopes.Add(new Radioisotope
                {
                    Id = isotopeId,
                    Symbol = "Co-60",
                    Name = "Cobalt-60",
                    ArabicName = "كوبالت-60",
                    HalfLife = 5.27,
                    RadiationType = "Gamma",
                    IsDeleted = true,
                    DeletedAt = DateTime.Now.AddDays(-3),
                    DeletedBy = adminUser.Id
                });
                db.SaveChanges();
            }

            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(u => u.CurrentUser).Returns(adminUser);
            var auditService = new AuditService(_fixture.ContextFactory, mockUserService.Object);
            var isotopeService = new RadioisotopeService(_fixture.ContextFactory, auditService, mockUserService.Object);

            // Act
            var (success, message) = isotopeService.Restore(isotopeId);

            // Assert
            Assert.True(success);
            Assert.True(message.Contains("Co-60") || message.Contains("Cobalt-60") || message.Contains("كوبالت-60"));

            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                var restored = db.Radioisotopes.FirstOrDefault(r => r.Id == isotopeId);
                Assert.NotNull(restored);
                Assert.False(restored.IsDeleted);
                Assert.Null(restored.DeletedAt);
                Assert.Null(restored.DeletedBy);

                // Verify AuditLog
                var log = db.AuditLogs.FirstOrDefault(a => a.TableName == "Radioisotopes" && a.RecordId == isotopeId && a.Action == "Restore");
                Assert.NotNull(log);
                Assert.NotNull(log.Details);
                Assert.True(log.Details.Contains("Co-60") || log.Details.Contains("Cobalt-60") || log.Details.Contains("كوبالت-60"));
            }
        }

        [Fact]
        public void RestoreSource_Fails_WhenAssociatedLocationIsDeleted()
        {
            // Arrange
            var deletedLocation = new Location { Id = Guid.NewGuid(), LocationName = "موقع محذوف سابقاً", IsDeleted = true, DeletedAt = DateTime.Now.AddDays(-10) };
            var isotope = new Radioisotope { Id = Guid.NewGuid(), Symbol = "Ir-192", Name = "Iridium-192", HalfLife = 73.83, RadiationType = "Gamma" };
            var unit = new ActivityUnit { Id = Guid.NewGuid(), UnitName = "GBq", UnitSymbol = "GBq", ConversionToBq = 1e9 };

            var sourceId = Guid.NewGuid();
            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                db.Locations.Add(deletedLocation);
                db.Radioisotopes.Add(isotope);
                db.ActivityUnits.Add(unit);
                db.Sources.Add(new Source
                {
                    Id = sourceId,
                    SourceCode = "SRC-ORPHAN-01",
                    LocationId = deletedLocation.Id,
                    RadioisotopeId = isotope.Id,
                    InitialActivityValue = 50,
                    InitialActivityUnitId = unit.Id,
                    CalibrationDate = DateTime.Now.AddYears(-1),
                    CurrentActivityValue = 40,
                    CurrentActivityUnitId = unit.Id,
                    Status = "InUse",
                    IsDeleted = true,
                    DeletedAt = DateTime.Now.AddDays(-2)
                });
                db.SaveChanges();
            }

            var adminUser = new User { Id = Guid.NewGuid(), Username = "admin_caller", Role = new Role { RoleName = "مدير النظام", Permissions = "All" }, Permissions = "All", IsEditor = true };
            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(u => u.CurrentUser).Returns(adminUser);
            var auditService = new AuditService(_fixture.ContextFactory, mockUserService.Object);
            var sourceService = new SourceService(_fixture.ContextFactory, new DecayCalculationService(), auditService, mockUserService.Object);

            // Act
            var (success, message) = sourceService.RestoreSource(sourceId);

            // Assert
            Assert.False(success);
            Assert.Contains("موقعه الأصلي", message);
            Assert.Contains("محذوف حالياً", message);
            Assert.Contains("موقع محذوف سابقاً", message);

            // Verify source remains deleted
            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                var src = db.Sources.IgnoreQueryFilters().First(s => s.Id == sourceId);
                Assert.True(src.IsDeleted);
            }
        }

        [Fact]
        public void RestoreUser_Fails_WhenUsernameConflictsWithActiveUser()
        {
            // Arrange
            var role = new Role { Id = Guid.NewGuid(), RoleName = "فني" };
            var adminRole = new Role { Id = Guid.NewGuid(), RoleName = "مدير النظام", Permissions = "All" };
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "admin_conflict_caller",
                RoleId = adminRole.Id,
                Role = adminRole,
                PasswordHash = PasswordHelper.HashPassword("Pass123!"),
                Permissions = "All",
                IsActive = true,
                IsEditor = true
            };
            var deletedUserId = Guid.NewGuid();
            var activeUserId = Guid.NewGuid();

            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                db.Roles.AddRange(role, adminRole);
                db.Users.Add(adminUser);

                // Deleted user with username 'same_user'
                db.Users.Add(new User
                {
                    Id = deletedUserId,
                    FullName = "مستخدم قديم محذوف",
                    Username = "same_user",
                    RoleId = role.Id,
                    IsDeleted = true,
                    DeletedAt = DateTime.Now.AddDays(-30)
                });

                // New active user with same username 'same_user'
                db.Users.Add(new User
                {
                    Id = activeUserId,
                    FullName = "مستخدم جديد نشط",
                    Username = "same_user",
                    RoleId = role.Id,
                    IsDeleted = false
                });

                db.SaveChanges();
            }

            var mockUserService = new Mock<IUserService>();
            mockUserService.Setup(u => u.CurrentUser).Returns(adminUser);
            var auditService = new AuditService(_fixture.ContextFactory, mockUserService.Object);
            var userService = new UserService(_fixture.ContextFactory, auditService);
            userService.Login("admin_conflict_caller", "Pass123!");

            // Act
            var (success, message) = userService.RestoreUser(deletedUserId);

            // Assert
            Assert.False(success);
            Assert.Contains("لوجود حساب نشط آخر بنفس اسم المستخدم", message);
            Assert.Contains("same_user", message);

            // Verify user remains deleted
            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                var usr = db.Users.IgnoreQueryFilters().First(u => u.Id == deletedUserId);
                Assert.True(usr.IsDeleted);
            }
        }

        [Fact]
        public async Task DeletionsViewModel_RestoreItemCommand_ExecutesSuccessfullyAndRefreshesList()
        {
            // Arrange
            var loc = new Location { Id = Guid.NewGuid(), LocationName = "موقع للاسترجاع الفوري", IsDeleted = true, DeletedAt = DateTime.Now.AddDays(-1) };
            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                db.Locations.Add(loc);
                db.SaveChanges();
            }

            var mockUser = new Mock<IUserService>();
            var adminUser = new User { Id = Guid.NewGuid(), Username = "admin", Role = new Role { RoleName = "مدير النظام", Permissions = "All" }, Permissions = "All", IsEditor = true };
            mockUser.Setup(u => u.CurrentUser).Returns(adminUser);

            var vm = new DeletionsViewModel(_fixture.ContextFactory, userService: mockUser.Object);
            await vm.LoadDeletedItemsAsync();

            var rowToRestore = vm.AllItems.FirstOrDefault(i => i.Id == loc.Id);
            Assert.NotNull(rowToRestore);

            DialogHelper.ShowConfirmationResult = true;

            try
            {
                // Act
                await vm.RestoreItemCommand.ExecuteAsync(rowToRestore);

                // Assert
                Assert.NotNull(DialogHelper.LastMessage);
                Assert.Contains("موقع للاسترجاع الفوري", DialogHelper.LastMessage);

                // Verify row disappeared from ViewModel lists
                Assert.DoesNotContain(vm.AllItems, i => i.Id == loc.Id);
                Assert.DoesNotContain(vm.FilteredItems, i => i.Id == loc.Id);
            }
            finally
            {
                DialogHelper.ShowConfirmationResult = null;
            }
        }

        [Fact]
        public async Task DeletionsViewModel_RestoreItemCommand_AbortedWhenUserCancelsConfirmation()
        {
            // Arrange
            var loc = new Location { Id = Guid.NewGuid(), LocationName = "موقع للإلغاء", IsDeleted = true, DeletedAt = DateTime.Now.AddDays(-1) };
            using (var db = _fixture.ContextFactory.CreateDbContext())
            {
                db.Locations.Add(loc);
                db.SaveChanges();
            }

            var vm = new DeletionsViewModel(_fixture.ContextFactory);
            await vm.LoadDeletedItemsAsync();

            var rowToRestore = vm.AllItems.FirstOrDefault(i => i.Id == loc.Id);
            Assert.NotNull(rowToRestore);

            // User clicks "No" on confirmation
            DialogHelper.ShowConfirmationResult = false;

            try
            {
                // Act
                await vm.RestoreItemCommand.ExecuteAsync(rowToRestore);

                // Assert: Location remains deleted and in the list
                Assert.Contains(vm.AllItems, i => i.Id == loc.Id);

                using (var db = _fixture.ContextFactory.CreateDbContext())
                {
                    var item = db.Locations.IgnoreQueryFilters().First(l => l.Id == loc.Id);
                    Assert.True(item.IsDeleted);
                }
            }
            finally
            {
                DialogHelper.ShowConfirmationResult = null;
            }
        }

        #endregion
    }
}
