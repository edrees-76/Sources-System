using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sources.Models;

namespace Sources.Services;

public interface IGlobalSearchService
{
    /// <summary>
    /// إجراء بحث موحّد متزامن بالتوازي في الكيانات الأربعة (المصادر، المواقع، المستخدمين، النظائر).
    /// </summary>
    /// <param name="query">نص البحث المدخل</param>
    /// <param name="cancellationToken">رمز الإلغاء</param>
    /// <returns>قائمة المجموعات المصنفة للنتائج</returns>
    Task<List<GlobalSearchResultGroup>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
