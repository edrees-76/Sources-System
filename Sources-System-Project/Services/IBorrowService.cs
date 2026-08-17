using System;
using System.Collections.Generic;
using Sources.Models;

namespace Sources.Services;

/// <summary>
/// Service interface for managing Source Borrowing requests.
/// </summary>
public interface IBorrowService
{
    List<BorrowRequest> GetAll();
    List<BorrowRequest> GetBySource(Guid sourceId);
    List<BorrowRequest> GetPending();
    List<BorrowRequest> GetOverdue();
    int GetPendingCount();
    
    (bool Success, string Message) CreateRequest(BorrowRequest request);
    (bool Success, string Message) MarkReturned(Guid requestId, Guid returnedByUserId, DateTime actualReturnDate, string? notes = null);
    
    void CheckAndUpdateOverdue();
    int GetDueSoonDaysThreshold();
    int GetDueSoonCount(IEnumerable<BorrowRequest>? requests = null);
    List<BorrowRequest> GetDueSoonRequests();
}
