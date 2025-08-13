document.getElementById("fetchDataButton").addEventListener("click", function () {
   alert("HERE");
    var startingDate = document.getElementById('datePicker');
    var selectedDate = document.getElementById('datePicker').value;
    var selectedDate2 = document.getElementById('datePicker2').value;
   if (selectedDate) {
        //document.getElementById("hiddenDateInput").value = selectedDate;
        //document.getElementById("hiddenDateInput2").value = selectedDate2;
        //document.getElementById("UploadButton").click();
    }
});

function changefunc() {
    var selectedDateVal = document.getElementById('datePicker').value;
    var selectedDate2 = document.getElementById('datePicker2');
    selectedDate2.value = selectedDateVal;
    selectedDate2.min = selectedDateVal;
    var maxDateVal = new Date();
 //   alert(maxDateVal);
    maxDateVal.setDate(maxDateVal.getDate() - 1);
 //   alert(maxDateVal);
    var maxDateStr = maxDateVal.toISOString().split("T")[0];
 //   alert(maxDateStr);
    selectedDate2.max = maxDateStr;
}