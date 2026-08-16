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
                    // Στελέχη (Officers)
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
                    // Ανθυπασπιστές & Υπαξιωματικοί
                    new Rank { Name = "Ανθυπασπιστής", ShortName = "Ανθστης", Category = PersonnelCategory.OfficerOrNco, SortOrder = 11 },
                    new Rank { Name = "Αρχιλοχίας", ShortName = "Αρχιας", Category = PersonnelCategory.OfficerOrNco, SortOrder = 12 },
                    new Rank { Name = "Επιλοχίας", ShortName = "Επχιας", Category = PersonnelCategory.OfficerOrNco, SortOrder = 13 },
                    new Rank { Name = "Λοχίας", ShortName = "Λχιας", Category = PersonnelCategory.OfficerOrNco, SortOrder = 14 },
                    new Rank { Name = "Δεκανέας", ShortName = "Δνεας", Category = PersonnelCategory.OfficerOrNco, SortOrder = 15 },
                    // Οπλίτες
                    new Rank { Name = "Υποδεκανέας", ShortName = "Υπδνεας", Category = PersonnelCategory.Conscript, SortOrder = 16 },
                    new Rank { Name = "Στρατιώτης", ShortName = "Στρ", Category = PersonnelCategory.Conscript, SortOrder = 17 },
                    new Rank { Name = "Μόνιμος Υπάλληλος", ShortName = "Μ.Υ.", Category = PersonnelCategory.Civilian, SortOrder = 18 }
                };

                foreach (var r in ranks)
                {
                    uow.Ranks.Insert(r);
                }
            }

            // 2. Seed Default Organisation Units
            if (uow.OrganisationUnits.Count() == 0)
            {
                var mainBattalion = new OrganisationUnit { Name = "Τάγμα / Μονάδα", Code = "ΜΟΝΑΔΑ", SortOrder = 1 };
                uow.OrganisationUnits.Insert(mainBattalion);

                var units = new[]
                {
                    new OrganisationUnit { Name = "1ος Λόχος", Code = "1ΛΧ", ParentUnitId = mainBattalion.Id, SortOrder = 2 },
                    new OrganisationUnit { Name = "2ος Λόχος", Code = "2ΛΧ", ParentUnitId = mainBattalion.Id, SortOrder = 3 },
                    new OrganisationUnit { Name = "3ος Λόχος", Code = "3ΛΧ", ParentUnitId = mainBattalion.Id, SortOrder = 4 },
                    new OrganisationUnit { Name = "Λόχος Διοικήσεως", Code = "ΛΔ", ParentUnitId = mainBattalion.Id, SortOrder = 5 },
                    new OrganisationUnit { Name = "1ο Γραφείο", Code = "1ΓΡ", ParentUnitId = mainBattalion.Id, SortOrder = 6 },
                    new OrganisationUnit { Name = "2ο Γραφείο", Code = "2ΓΡ", ParentUnitId = mainBattalion.Id, SortOrder = 7 },
                    new OrganisationUnit { Name = "3ο Γραφείο", Code = "3ΓΡ", ParentUnitId = mainBattalion.Id, SortOrder = 8 },
                    new OrganisationUnit { Name = "4ο Γραφείο", Code = "4ΓΡ", ParentUnitId = mainBattalion.Id, SortOrder = 9 }
                };

                foreach (var u in units)
                {
                    uow.OrganisationUnits.Insert(u);
                }
            }

            // 3. Seed Status Types (Απουσίες & Μεταβολές)
            if (uow.StatusTypes.Count() == 0)
            {
                var types = new[]
                {
                    new StatusType { Name = "Κανονική Άδεια", ShortCode = "ΚΑ", Effect = StatusEffect.Absent, ReportMappingCode = "KA", DisplayColorHex = "#2563EB", SortOrder = 1 },
                    new StatusType { Name = "Αναρρωτική Άδεια", ShortCode = "ΑΑ", Effect = StatusEffect.Absent, ReportMappingCode = "AA", DisplayColorHex = "#DC2626", SortOrder = 2 },
                    new StatusType { Name = "Τιμητική Άδεια", ShortCode = "ΤΑ", Effect = StatusEffect.Absent, ReportMappingCode = "TA", DisplayColorHex = "#059669", SortOrder = 3 },
                    new StatusType { Name = "Φύλλο Πορείας / Μετάθεση", ShortCode = "ΦΠ", Effect = StatusEffect.Absent, ReportMappingCode = "FP", DisplayColorHex = "#7C3AED", SortOrder = 4 },
                    new StatusType { Name = "Νοσηλεία / 401 ΓΣΝΑ", ShortCode = "ΝΟΣ", Effect = StatusEffect.Absent, ReportMappingCode = "NOS", DisplayColorHex = "#E11D48", SortOrder = 5 },
                    new StatusType { Name = "Απόσπαση", ShortCode = "ΑΠΟΣΠ", Effect = StatusEffect.Absent, ReportMappingCode = "APOSP", DisplayColorHex = "#D97706", SortOrder = 6 },
                    new StatusType { Name = "Ειδική Άδεια", ShortCode = "ΕΙΔ", Effect = StatusEffect.Absent, ReportMappingCode = "EID", DisplayColorHex = "#0891B2", SortOrder = 7 },
                    new StatusType { Name = "Φυλακή / Πειθαρχική Ποινή", ShortCode = "ΦΥΛ", Effect = StatusEffect.Absent, ReportMappingCode = "FYL", DisplayColorHex = "#4B5563", SortOrder = 8 },
                    new StatusType { Name = "Εκτός Έδρας / Υπηρεσιακή Μετακίνηση", ShortCode = "ΕΚΤΟΣ", Effect = StatusEffect.Absent, ReportMappingCode = "EKTOS", DisplayColorHex = "#4F46E5", SortOrder = 9 }
                };

                foreach (var st in types)
                {
                    uow.StatusTypes.Insert(st);
                }
            }

            // 4. Seed Service Types (Υπηρεσίες)
            if (uow.ServiceTypes.Count() == 0)
            {
                var services = new[]
                {
                    new ServiceType { Name = "Αξιωματικός Υπηρεσίας", ShortCode = "ΑΞΥΠ", SortOrder = 1 },
                    new ServiceType { Name = "Βοηθός Αξ/κου Υπηρεσίας", ShortCode = "ΒΑΞΥΠ", SortOrder = 2 },
                    new ServiceType { Name = "Επόπτης Ασφαλείας", ShortCode = "ΕΠΟΠΤΗΣ", SortOrder = 3 },
                    new ServiceType { Name = "Αρχιφύλακας", ShortCode = "ΑΡΧΙΦ", SortOrder = 4 },
                    new ServiceType { Name = "Σκοπός Κεντρικής Πύλης", ShortCode = "ΣΚΟΠΟΣ ΠΥΛΗΣ", SortOrder = 5 },
                    new ServiceType { Name = "Σκοπός Αποθήκης Πυρομαχικών", ShortCode = "ΣΚΟΠΟΣ ΠΥΡ", SortOrder = 6 },
                    new ServiceType { Name = "Θαλαμοφύλακας", ShortCode = "ΘΑΛΑΜ", SortOrder = 7 },
                    new ServiceType { Name = "Περίπολος", ShortCode = "ΠΕΡΙΠ", SortOrder = 8 },
                    new ServiceType { Name = "Μαγειρείο / Εστιάτορας", ShortCode = "ΕΣΤΙΑΤ", SortOrder = 9 },
                    new ServiceType { Name = "Οδηγός Υπηρεσίας / Επιφυλακής", ShortCode = "ΟΔΗΓΟΣ", SortOrder = 10 }
                };

                foreach (var s in services)
                {
                    uow.ServiceTypes.Insert(s);
                }
            }
        }
    }
}
