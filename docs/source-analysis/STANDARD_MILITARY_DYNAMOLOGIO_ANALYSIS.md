# Source Document Reverse-Engineering Analysis: Standard Military DYNAMOLOGIO

## 1. Overview
The official Hellenic military and administrative daily strength report (**ΗΜΕΡΗΣΙΟ ΔΥΝΑΜΟΛΟΓΙΟ**) serves as the legal and operational record of daily unit strength, presence, absence, and duty roster execution.

## 2. Workbook Topography
- **File Format**: Standard `.xlsx` / `.xls` (A4 Landscape / Portrait)
- **Primary Worksheets**:
  1. `ΔΥΝΑΜΟΛΟΓΙΟ`: Executive summary grid containing active strength totals, present, absent breakdowns by reason (ΚΑ, ΑΑ, ΦΠ, Νοσηλεία, Ειδικές, κλπ.) segmented into Στελέχη (Officers/NCOs) and Οπλίτες (Soldiers/Conscripts).
  2. `ΚΑΤΑΣΤΑΣΗ ΑΠΟΝΤΩΝ`: Detailed nominal list of all absent personnel with Rank, Full Name, Reason, Date of Departure, Date of Return, and Authorization Order.
  3. `ΥΠΗΡΕΣΙΕΣ`: Daily duty assignments (Αξ/κος Υπηρεσίας, Επόπτης, Αρχιφύλακας, Σκοποί, Θαλαμοφύλακες).

## 3. Cell Mapping Specifications & Formats
- **Report Date**: Cell `[B2]` / `[C2]` formatted as `dd/MM/yyyy`
- **Unit Title**: Cell `[B1]` (e.g. "ΣΤΡΑΤΟΣ ΞΗΡΑΣ - 123 ΤΑΓΜΑ ΠΕΖΙΚΟΥ - 1ο ΓΡΑΦΕΙΟ")
- **Strength Summary Matrix**:
  - `Rows`: Ranks and Categories (ΣΤΕΛΕΧΗ: Αξ/κοι, Ανθστές, Υπαξ/κοι, ΕΠΟΠ - ΟΠΛΙΤΕΣ: Στρατιώτες)
  - `Columns`:
    - `ΥΠΑΡΧΟΥΣΑ ΔΥΝΑΜΗ` (Active Strength)
    - `ΠΑΡΟΝΤΕΣ` (Present Count)
    - `ΑΠΟΝΤΕΣ ΣΥΝΟΛΟ` (Total Absent)
    - `ΑΝΑΛΥΣΗ ΑΠΟΥΣΙΩΝ`: `ΚΑ` (Κανονική), `ΑΑ` (Αναρρωτική), `ΦΠ` (Φύλλο Πορείας), `ΝΟΣ` (Νοσηλεία), `ΑΠΟΣΠ` (Απόσπαση), `ΕΙΔ` (Ειδική), `ΛΟΙΠΟΙ` (Other)
- **Mathematical Invariant Check**: Total Present + Total Absent = Active Strength.

## 4. Preservation Policy
The output writer opens the golden template from `templates-reference/Standard_Dynamologio_Template.xlsx`, fills only mapped cells, preserves all formulas (e.g. `=SUM(...)`), styles, borders, and print margins, and saves to the destination file.
