using System;
using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

public interface IAlertService
{
    List<AlertNotification> GenerateAlerts();
    List<AlertNotification> GetActiveAlerts();
    List<AlertNotification> GetAllAlerts(bool includeDismissed = true);
    int GetUnreadCount();
    void MarkAsRead(Guid alertId);
    void DismissAlert(Guid alertId);
    void MarkAllAsRead();
}
