# Domain Specification: ΔΥΝΑΜΟΛΟΓΙΟ (docs/DOMAIN.md)

## 1. Domain Entities & Value Objects

### 1.1 `Personnel` (Προσωπικό)
- **`Id`**: `Guid` (Primary Key)
- **`MilitaryServiceNumber`**: `string` (ΑΣΜ / Αριθμός Στρατιωτικού Μητρώου ή ΣΠΑ)
- **`LastName`**: `string` (Επώνυμο - e.g. ΠΑΠΑΔΟΠΟΥΛΟΣ)
- **`FirstName`**: `string` (Όνομα - e.g. ΙΩΑΝΝΗΣ)
- **`FatherName`**: `string` (Πατρώνυμο - e.g. ΓΕΩΡΓΙΟΣ)
- **`RankId`**: `Guid` (Foreign Key to `Rank`)
- **`Category`**: `PersonnelCategory` Enum (`OfficerOrNco` [Στέλεχος], `Conscript` [Οπλίτης], `Civilian` [Πολιτικό Προσωπικό])
- **`OrganisationUnitId`**: `Guid` (Foreign Key to `OrganisationUnit`)
- **`CompanyOrSection`**: `string` (Λόχος / Διμοιρία / Γραφείο)
- **`Specialty`**: `string` (Ειδικότητα)
- **`StrengthStartDate`**: `DateTime` (Ημερομηνία Ένταξης στη Δύναμη)
- **`StrengthEndDate`**: `DateTime?` (Ημερομηνία Διαγραφής / Μετάθεσης / Απόλυσης - null if currently active)
- **`IsArchived`**: `bool` (Αρχειοθετημένος)
- **`Notes`**: `string` (Παρατηρήσεις)
- **`CreatedAt`**, **`ModifiedAt`**, **`CreatedBy`**, **`ModifiedBy`**: Audit timestamps & user IDs.

### 1.2 `Rank` (Βαθμός)
- **`Id`**: `Guid`
- **`Name`**: `string` (Πλήρης Τίτλος - e.g. "Λοχαγός", "Επιλοχίας", "Στρατιώτης")
- **`ShortName`**: `string` (Συντομογραφία - e.g. "Λγος", "Επχιας", "Στρ (ΠΖ)")
- **`Category`**: `PersonnelCategory`
- **`SortOrder`**: `int` (Seniority index for report sorting: 1 = General down to 30 = Private)
- **`IsActive`**: `bool`

### 1.3 `OrganisationUnit` (Μονάδα / Υπομονάδα / Γραφείο)
- **`Id`**: `Guid`
- **`Name`**: `string` (e.g. "1ος Λόχος", "Λόχος Διοικήσεως", "1ο Γραφείο", "Διμοιρία Διαβιβάσεων")
- **`Code`**: `string`
- **`ParentUnitId`**: `Guid?` (Hierarchical tree structure)
- **`SortOrder`**: `int`
- **`IsActive`**: `bool`

### 1.4 `StatusType` (Τύπος Κατάστασης / Απουσίας)
- **`Id`**: `Guid`
- **`Name`**: `string` (e.g. "Κανονική Άδεια", "Αναρρωτική Άδεια", "Φύλλο Πορείας", "Νοσηλεία")
- **`ShortCode`**: `string` (e.g. "ΚΑ", "ΑΑ", "ΦΠ", "ΝΟΣ")
- **`Effect`**: `StatusEffect` Enum (`Present`, `Absent`, `ExcludedFromStrength`)
- **`RequiresEndDate`**: `bool`
- **`MutualExclusionGroup`**: `string` (Prevents conflicting overlaps within the same group)
- **`ReportMappingCode`**: `string` (Maps to specific column/bucket in official DYNAMOLOGIO report)
- **`DisplayColorHex`**: `string` (Hex color for UI badge/chip, e.g. "#D97706")
- **`SortOrder`**: `int`
- **`IsActive`**: `bool`

### 1.5 `StatusEvent` (Γεγονός Κατάστασης / Μεταβολή)
- **`Id`**: `Guid`
- **`PersonnelId`**: `Guid`
- **`StatusTypeId`**: `Guid`
- **`StartAt`**: `DateTime` (Inclusive start: 16/08/2026 00:00)
- **`EndAtExclusive`**: `DateTime` (Exclusive return: 21/08/2026 00:00 -> Person is absent 16-20, present on 21st)
- **`IsAllDay`**: `bool`
- **`ReferenceDocument`**: `string` (Αρ. Διαταγής / Έγγραφο Έγκρισης)
- **`Comment`**: `string` (Αιτιολογία / Παρατηρήσεις)
- **`IsCancelled`**: `bool` (Soft cancellation to preserve audit history)
- **`CancellationReason`**: `string`
- **`ImportBatchId`**: `Guid?` (Provenance tracking)

### 1.6 `ServiceType` (Είδος Υπηρεσίας)
- **`Id`**: `Guid`
- **`Name`**: `string` (e.g. "Αξιωματικός Υπηρεσίας", "Επόπτης Ασφαλείας", "Αρχιφύλακας", "Σκοπός")
- **`ShortCode`**: `string`
- **`DefaultStartTime`**: `TimeSpan`
- **`DefaultEndTime`**: `TimeSpan`
- **`AffectsPresence`**: `bool` (False for standard internal services, True if 24hr external detachment)
- **`SortOrder`**: `int`
- **`IsActive`**: `bool`

### 1.7 `ServiceAssignment` (Ανάθεση Υπηρεσίας)
- **`Id`**: `Guid`
- **`PersonnelId`**: `Guid`
- **`ServiceTypeId`**: `Guid`
- **`ServiceDate`**: `DateTime`
- **`StartDateTime`**: `DateTime`
- **`EndDateTime`**: `DateTime`
- **`DutyLocation`**: `string` (e.g. "Κεντρική Πύλη", "Φυλάκιο 1", "Διοικητήριο")
- **`Notes`**: `string`
- **`IsCancelled`**: `bool`

---

## 2. Calculated Projections & Invariants

### 2.1 Calculated Presence Query
Given timestamp $T$ and person $P$:
1. Active in strength: $P.\text{StrengthStartDate} \le T$ and ($P.\text{StrengthEndDate} == \text{null}$ or $T < P.\text{StrengthEndDate}$) and $!P.\text{IsArchived}$.
2. Active Status Events: Find all non-cancelled `StatusEvent` where $E.\text{StartAt} \le T < E.\text{EndAtExclusive}$.
3. Resulting State:
   - If not active in strength $\to$ **`Excluded`**
   - If active `StatusEvent` exists with `StatusEffect.Absent` $\to$ **`Absent`** (with reason & return date $E.\text{EndAtExclusive}$)
   - If active `StatusEvent` exists with `StatusEffect.ExcludedFromStrength` $\to$ **`Excluded`**
   - Otherwise $\to$ **`Present`**

### 2.2 Mathematical Invariants
$$\text{Active Strength}(T) = \text{Total Active Personnel}(T)$$
$$\text{Present Count}(T) + \text{Absent Count}(T) = \text{Active Strength}(T)$$
$$\text{Active Strength}(T) = \sum_{\text{categories}} \text{Active Personnel}(\text{category}, T)$$
