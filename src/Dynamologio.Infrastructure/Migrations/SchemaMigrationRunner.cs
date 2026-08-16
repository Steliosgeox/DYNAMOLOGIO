using System;
using System.Linq;
using Dynamologio.Core.Enums;
using Dynamologio.Core.Interfaces;
using Dynamologio.Core.Models;

namespace Dynamologio.Infrastructure.Migrations
{
    public class SchemaMigrationRunner
    {
        public const int CurrentSchemaVersion = 1;
        private readonly IUnitOfWork _uow;

        public SchemaMigrationRunner(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public void RunMigrations()
        {
            ApplyMigrations(_uow);
        }

        public static void ApplyMigrations(IUnitOfWork uow)
        {
            var versionSetting = uow.AppSettings.Find(s => s.Key == "SchemaVersion").FirstOrDefault();
            int currentVersion = 0;
            if (versionSetting != null && int.TryParse(versionSetting.Value, out var v))
            {
                currentVersion = v;
            }

            if (currentVersion < 1)
            {
                ApplyMigrationV1(uow);

                if (versionSetting == null)
                {
                    uow.AppSettings.Insert(new AppSetting
                    {
                        Key = "SchemaVersion",
                        Value = "1",
                        Description = "Έκδοση Σχήματος Βάσης Δεδομένων"
                    });
                }
                else
                {
                    versionSetting.Value = "1";
                    uow.AppSettings.Update(versionSetting);
                }
            }
        }

        private static void ApplyMigrationV1(IUnitOfWork uow)
        {
            // 1. Seed Ranks (Στελέχη & Οπλίτες)
            if (uow.Ranks.Count() == 0)
            {
                var ranks = new[]
                {
                    new Rank { Name = "Στρατηγός", ShortName = "Στγος", Category = PersonnelCategory.OfficerOrNco, SortOrder = 1 },
                    new Rank { Name = "Αντιστράτηγος", ShortName = "Αντγος", Category = PersonnelCategory.OfficerOrNco, SortOrder = 2 },
                    new Rank { Name = "Υποστράτηγος", ShortName = "Υπτγος", Category = PersonnelCategory.OfficerOrNco, SortOrder = 3 },
                    new Rank { Name = "Ταξίαρχος", ShortName = "Ταξχος", Category = PersonnelCategory.OfficerOrNco, SortOrder = 4 },
                    new Rank { Name = "Συνταγματάρχης", ShortName = "Σχης", Category = PersonnelCategory.OfficerOrNco, SortOrder = 5 },
                    new Rank { Name = "Αντισυνταγματάρχης", ShortName = "Ανχης", Category = PersonnelCategory.OfficerOrNco, SortOrder = 6 },
                    new Rank { Name = "Ταγματάρχης", ShortName = "Τχης", Category = PersonnelCategory.OfficerOrNco, SortOrder = 7 },
                    new Rank { Name = "Λοχαγός", ShortName = "Λγος", Category = PersonnelCategory.OfficerOrNco, SortOrder = 8 },
                    new Rank { Name = "Υπολοχαγός", ShortName = "Υπλγος", Category = PersonnelCategory.OfficerOrNco, SortOrder = 9 },
                    new Rank { Name = "Ανθυπολοχαγός", ShortName = "Ανθλγος", Category = PersonnelCategory.OfficerOrNco, SortOrder = 10 },
                    new Rank { Name = "Ανθυπασπιστής", ShortName = "Ανθστης", Category = PersonnelCategory.OfficerOrNco, SortOrder = 11 },
                    new Rank { Name = "Αρχιλοχίας", ShortName = "Αρχιας", Category = PersonnelCategory.OfficerOrNco, SortOrder = 12 },
                    new Rank { Name = "Επιλοχίας", ShortName = "Επχιας", Category = PersonnelCategory.OfficerOrNco, SortOrder = 13 },
                    new Rank { Name = "Λοχίας", ShortName = "Λχιας", Category = PersonnelCategory.OfficerOrNco, SortOrder = 14 },
                    new Rank { Name = "Δεκανέας", ShortName = "Δνεας", Category = PersonnelCategory.Conscript, SortOrder = 15 },
                    new Rank { Name = "Υποδεκανέας", ShortName = "Υπδνεας", Category = PersonnelCategory.Conscript, SortOrder = 16 },
                    new Rank { Name = "Στρατιώτης", ShortName = "Στρ", Category = PersonnelCategory.Conscript, SortOrder = 17 },
                    new Rank { Name = "Μόνιμος Πολιτικός Υπάλληλος", ShortName = "Π.Υ.", Category = PersonnelCategory.Civilian, SortOrder = 18 }
                };

                foreach (var r in ranks) uow.Ranks.Insert(r);
            }

            // 2. Seed Default Units
            if (uow.OrganisationUnits.Count() == 0)
            {
                var units = new[]
                {
                    new OrganisationUnit { Name = "Λόχος Διοικήσεως (ΛΔ)", Code = "ΛΔ", SortOrder = 1 },
                    new OrganisationUnit { Name = "1ος Λόχος Τυφεκιοφόρων", Code = "1ος ΛΤ", SortOrder = 2 },
                    new OrganisationUnit { Name = "2ος Λόχος Τυφεκιοφόρων", Code = "2ος ΛΤ", SortOrder = 3 },
                    new OrganisationUnit { Name = "3ος Λόχος Τυφεκιοφόρων", Code = "3ος ΛΤ", SortOrder = 4 },
                    new OrganisationUnit { Name = "Λόχος Υποστηρίξεως (ΛΥΠ)", Code = "ΛΥΠ", SortOrder = 5 }
                };

                foreach (var u in units) uow.OrganisationUnits.Insert(u);
            }

            // 3. Seed Absence / Status Types
            if (uow.StatusTypes.Count() == 0)
            {
                var types = new[]
                {
                    new StatusType { Name = "Κανονική Άδεια (ΚΑ)", ShortCode = "ΚΑ", Effect = StatusEffect.Absent, MutualExclusionGroup = "LEAVE", ReportMappingCode = "KA", SortOrder = 1 },
                    new StatusType { Name = "Αναρρωτική Άδεια (ΑΑ)", ShortCode = "ΑΑ", Effect = StatusEffect.Absent, MutualExclusionGroup = "LEAVE", ReportMappingCode = "AA", SortOrder = 2 },
                    new StatusType { Name = "Τιμητική Άδεια (ΤΑ)", ShortCode = "ΤΑ", Effect = StatusEffect.Absent, MutualExclusionGroup = "LEAVE", ReportMappingCode = "TA", SortOrder = 3 },
                    new StatusType { Name = "Φοιτητική Άδεια (ΦΑ)", ShortCode = "ΦΑ", Effect = StatusEffect.Absent, MutualExclusionGroup = "LEAVE", ReportMappingCode = "FA", SortOrder = 4 },
                    new StatusType { Name = "Φύλλο Πορείας (ΦΠ)", ShortCode = "ΦΠ", Effect = StatusEffect.Absent, MutualExclusionGroup = "DETACHMENT", ReportMappingCode = "FP", SortOrder = 5 },
                    new StatusType { Name = "Νοσηλεία (ΝΟΣ)", ShortCode = "ΝΟΣ", Effect = StatusEffect.Absent, MutualExclusionGroup = "HOSPITAL", ReportMappingCode = "HOSP", SortOrder = 6 },
                    new StatusType { Name = "Απόσπαση (ΑΠΟΣΠ)", ShortCode = "ΑΠΟΣΠ", Effect = StatusEffect.Absent, MutualExclusionGroup = "DETACHMENT", ReportMappingCode = "DET", SortOrder = 7 },
                    new StatusType { Name = "Φυλακή (ΦΥΛ)", ShortCode = "ΦΥΛ", Effect = StatusEffect.Absent, MutualExclusionGroup = "PENALTY", ReportMappingCode = "PRIS", SortOrder = 8 },
                    new StatusType { Name = "Εκτός Έδρας (ΕΚΤΟΣ)", ShortCode = "ΕΚΤΟΣ", Effect = StatusEffect.Absent, MutualExclusionGroup = "DUTY_OUT", ReportMappingCode = "OUT", SortOrder = 9 }
                };

                foreach (var st in types) uow.StatusTypes.Insert(st);
            }

            // 4. Seed Service Types
            if (uow.ServiceTypes.Count() == 0)
            {
                var services = new[]
                {
                    new ServiceType { Name = "Αξιωματικός Υπηρεσίας (ΑΥ)", ShortCode = "ΑΥ", SortOrder = 1 },
                    new ServiceType { Name = "Βοηθός Αξιωματικού Υπηρεσίας (ΒΑΥ)", ShortCode = "ΒΑΥ", SortOrder = 2 },
                    new ServiceType { Name = "Αξιωματικός Φυλακής (ΑΞΦΥΛ)", ShortCode = "ΑΞΦΥΛ", SortOrder = 3 },
                    new ServiceType { Name = "Επόπτης Ασφαλείας", ShortCode = "ΕΠΟΠΤ", SortOrder = 4 },
                    new ServiceType { Name = "Αρχιφύλακας Κεντρικής Πύλης", ShortCode = "ΑΡΧΦΥΛ", SortOrder = 5 },
                    new ServiceType { Name = "Σκοπός Πύλης", ShortCode = "ΣΚΟΠ", SortOrder = 6 },
                    new ServiceType { Name = "Περίπολος Ασφαλείας", ShortCode = "ΠΕΡΙΠ", SortOrder = 7 },
                    new ServiceType { Name = "Θαλαμοφύλακας", ShortCode = "ΘΑΛ", SortOrder = 8 },
                    new ServiceType { Name = "Οδηγός Υπηρεσίας", ShortCode = "ΟΔΗΓ", SortOrder = 9 }
                };

                foreach (var sv in services) uow.ServiceTypes.Insert(sv);
            }
        }
    }
}
