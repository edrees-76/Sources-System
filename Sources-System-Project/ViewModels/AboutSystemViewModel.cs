using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sources.ViewModels
{
    public class AboutSystemViewModel : ObservableObject
    {
        public string SystemName => "مصادر - Sources Radioactive Source Tracking System";
        public string Version => "Version 1.0.0 (Pro)";
        
        public string DesignerName => "Eng. Edrees F. El-Hery";
        public string DesignerPhone1 => "+218 92 512 6355";
        public string DesignerPhone2 => "+218 91 773 0110";
        public string DesignerEmail => "edreeselhery@gmail.com";
        
        public AboutSystemViewModel()
        {
        }
    }
}
