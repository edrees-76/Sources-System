using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sources.ViewModels
{
    public class AboutSystemViewModel : ObservableObject
    {
        public string SystemName => "SOURCES - منظومة تتبع المصادر المشعة";
        public string SystemNameEn => "Sources — Radioactive Source Tracking System";
        public string Version => "1.0.0 (Release 2026)";
        public string ReleaseYear => "2026";
        public string FrameworkTech => ".NET 8 / WPF";
        public string ComplianceStandard => "IAEA RS-G-1.9";
        public string DatabaseEngine => "SQLite 3";
        public string BeneficiaryInstitution => "مركز البحوث النووية - تاجوراء (TNRC)";

        public string DesignerName => "Eng. Edrees F. El-Hery";
        public string DesignerPhone1 => "+218 92 512 6355";
        public string DesignerPhone2 => "+218 91 773 0110";
        public string DesignerEmail => "edreeselhery@gmail.com";

        public AboutSystemViewModel()
        {
        }
    }
}
