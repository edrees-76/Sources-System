namespace Sources.Interfaces
{
    public interface IEditableViewModel
    {
        bool IsEditing { get; }

        /// <summary>
        /// يعيد ضبط حالة التحرير بلا حفظ (الجولة 199 — حارس الشفاء الذاتي). يُستدعى فقط عندما
        /// يكون IsEditing عالقاً true بلا نافذة تحرير فعلية مفتوحة (EditingFormTracker.IsFormOpen
        /// يعيد false)؛ يُطبَّق عبر أمر الإلغاء الحالي لكل شاشة (CancelEditCommand)، الذي يقتصر
        /// تحقّقاً على IsEditing=false + مسح النموذج، بلا حفظ وبلا نافذة تأكيد.
        /// </summary>
        void CancelEditing();
    }
}
