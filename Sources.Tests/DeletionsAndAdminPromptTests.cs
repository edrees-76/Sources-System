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
            PasswordPromptDialog.CustomPromptResult = null;
        }

        public void Dispose()
        {
            _fixture.ResetDatabase();
            DialogHelper.IsTestMode = false;
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
    }
}
