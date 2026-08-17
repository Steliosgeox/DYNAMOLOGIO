using Xunit;
using System.Collections.Generic;
using Dynamologio.Core.Models;
using Dynamologio.App.Controls;
using System.Threading;
using System.Windows.Threading;

namespace Dynamologio.Tests
{
    public class PersonnelLookupTests
    {
        [Fact]
        public void AT_UI_001_SearchablePersonPicker_SearchesAllFiveDimensions()
        {
            var staThread = new Thread(() =>
            {
                var picker = new SearchablePersonPicker();
                var items = new List<PersonnelLookupItem>
                {
                    new PersonnelLookupItem { RankName = "Λοχαγός", FullName = "ΠΑΠΑΔΟΠΟΥΛΟΣ ΝΙΚΟΛΑΟΣ", UnitName = "1ος ΛΟΧΟΣ", MilitaryServiceNumber = "12345", Specialty = "ΤΕΘΩΡΑΚΙΣΜΕΝΑ" },
                    new PersonnelLookupItem { RankName = "Στρατιώτης", FullName = "ΓΕΩΡΓΙΟΥ ΓΕΩΡΓΙΟΣ", UnitName = "2ος ΛΟΧΟΣ", MilitaryServiceNumber = "67890", Specialty = "ΤΥΦΕΚΙΟΦΟΡΟΣ" }
                };

                picker.RebuildItemList(items);

                // 1. Search by Rank
                picker.ApplyFilter("Λοχαγός");
                Assert.Single(picker.FilteredItems);
                Assert.Equal("ΠΑΠΑΔΟΠΟΥΛΟΣ ΝΙΚΟΛΑΟΣ", picker.FilteredItems[0].DisplayFullName);

                // 2. Search by FullName
                picker.ApplyFilter("ΓΕΩΡΓΙΟΥ");
                Assert.Single(picker.FilteredItems);
                Assert.Equal("67890", picker.FilteredItems[0].DisplayAsm);

                // 3. Search by Unit
                picker.ApplyFilter("1ος ΛΟΧΟΣ");
                Assert.Single(picker.FilteredItems);
                Assert.Equal("12345", picker.FilteredItems[0].DisplayAsm);

                // 4. Search by ASM
                picker.ApplyFilter("67890");
                Assert.Single(picker.FilteredItems);
                Assert.Equal("ΓΕΩΡΓΙΟΥ ΓΕΩΡΓΙΟΣ", picker.FilteredItems[0].DisplayFullName);

                // 5. Search by Specialty
                picker.ApplyFilter("ΤΕΘΩΡΑΚΙΣΜΕΝΑ");
                Assert.Single(picker.FilteredItems);
                Assert.Equal("ΠΑΠΑΔΟΠΟΥΛΟΣ ΝΙΚΟΛΑΟΣ", picker.FilteredItems[0].DisplayFullName);

                // 6. Reset search / Empty query
                picker.ApplyFilter("");
                Assert.Equal(2, picker.FilteredItems.Count);
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();
        }
    }
}
