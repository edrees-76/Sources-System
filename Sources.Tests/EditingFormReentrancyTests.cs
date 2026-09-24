using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using Moq;
using Sources.Models;
using Sources.Services;
using Sources.Tests.Fixtures;
using Sources.ViewModels;
using Sources.Views;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// اختبار حارس إعادة الدخول (الجولة 199 — B1): محاولة ثانية لفتح نافذة تحرير أثناء فتح نافذة
/// أخرى فعلاً يجب أن تُتجاهل. UsersView مُمثِّل للأنماط الخمسة المتطابقة (Users, Locations,
/// Radioisotopes, Borrow, وSourcesView عبر HandleIsEditingChanged). يُستدعى التابع الخاص
/// OpenForm مباشرة عبر الانعكاس بعد تثبيت _formWindow يدوياً على نافذة وهمية غير معروضة إطلاقاً،
/// حتى لا يُستدعى ShowDialog الحقيقي أبداً (يحجب التنفيذ إلى الأبد بلا تفاعل مستخدم).
/// </summary>
public class EditingFormReentrancyTests
{
    [Fact]
    public void OpenForm_WhenFormAlreadyOpen_IsIgnored_AndDoesNotReplaceExistingWindow()
    {
        WpfStaFixture.RunInSta(() =>
        {
            var reportingServiceMock = new Mock<IReportingService>();
            var userServiceMock = new Mock<IUserService>();
            userServiceMock.Setup(s => s.GetAllUsers()).Returns(new List<User>());
            userServiceMock.Setup(s => s.GetAllRoles()).Returns(new List<Role>());
            userServiceMock.Setup(s => s.GetAuditLogs(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .Returns(new List<AuditLog>());
            var vm = new UsersViewModel(userServiceMock.Object, reportingServiceMock.Object);

            var view = new UsersView();
            var formField = typeof(UsersView).GetField("_formWindow", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(formField);

            // نافذة وهمية لا تُعرض إطلاقاً (لا Show ولا ShowDialog): تُحاكي "نافذة تحرير مفتوحة
            // فعلاً" دون حجب الاختبار. النوع يجب أن يطابق حقل _formWindow (UserFormWindow؟).
            var sentinelWindow = new UserFormWindow();
            formField!.SetValue(view, sentinelWindow);

            var openFormMethod = typeof(UsersView).GetMethod("OpenForm", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(openFormMethod);

            // Act: محاولة فتح ثانية بينما _formWindow ليست null.
            openFormMethod!.Invoke(view, new object[] { vm });

            // Assert: لم تُستبدل النافذة الوهمية (لم يُنشأ UserFormWindow حقيقي، ولم يُستدعَ ShowDialog).
            var currentFormWindow = formField.GetValue(view);
            Assert.Same(sentinelWindow, currentFormWindow);
        });
    }
}
