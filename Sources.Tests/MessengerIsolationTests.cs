using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using Sources.Messages;
using Sources.Models;
using Sources.Services;
using Sources.ViewModels;
using Xunit;

namespace Sources.Tests;

/// <summary>
/// الجولة 198-E: يتحقق أن اثنتين من AlertsViewModel، عند بنائهما بوسيطين منفصلين
/// (IMessenger مستقلَّين بدل WeakReferenceMessenger.Default المشترك)، لا يتلقى أيّهما
/// رسائل الآخر — وهذا هو أساس عزل اختبارات النمط المطبَّق في الجولة 115 (BorrowViewModel).
/// </summary>
public class MessengerIsolationTests
{
    [Fact]
    public void TwoAlertsViewModels_WithSeparateMessengers_DoNotReceiveEachOthersMessages()
    {
        var messenger1 = new WeakReferenceMessenger();
        var messenger2 = new WeakReferenceMessenger();

        var mockAlertService1 = new Mock<IAlertService>();
        mockAlertService1.Setup(s => s.GetAllAlerts(It.IsAny<bool>())).Returns(new List<AlertNotification>());
        var mockLocationService1 = new Mock<ILocationService>();
        mockLocationService1.Setup(s => s.GetAll()).Returns(new List<Location>());

        var mockAlertService2 = new Mock<IAlertService>();
        mockAlertService2.Setup(s => s.GetAllAlerts(It.IsAny<bool>())).Returns(new List<AlertNotification>());
        var mockLocationService2 = new Mock<ILocationService>();
        mockLocationService2.Setup(s => s.GetAll()).Returns(new List<Location>());

        var vm1 = new AlertsViewModel(mockAlertService1.Object, mockLocationService1.Object, sourceService: null, messenger: messenger1);
        var vm2 = new AlertsViewModel(mockAlertService2.Object, mockLocationService2.Object, sourceService: null, messenger: messenger2);
        try
        {
            // كل بناء يستدعي LoadData مرة واحدة (تحميل أولي)
            mockAlertService1.Invocations.Clear();
            mockAlertService2.Invocations.Clear();

            // Act: إرسال رسالة على الوسيط الأول فقط
            messenger1.Send(new SourcesUpdatedMessage());

            // Assert: VM1 (المسجَّل على messenger1) يعيد التحميل، وVM2 (المسجَّل على messenger2) لا يتأثر إطلاقاً
            mockAlertService1.Verify(s => s.GetAllAlerts(It.IsAny<bool>()), Times.Once);
            mockAlertService2.Verify(s => s.GetAllAlerts(It.IsAny<bool>()), Times.Never);
        }
        finally
        {
            vm1.Dispose();
            vm2.Dispose();
        }
    }
}
