// UI elements to work with
const signInButton = document.getElementById('signIn');
const signOutButton = document.getElementById('signOut');
const editProfileButton = document.getElementById('editProfileButton');
const label = document.getElementById('label');
const responseLog = document.getElementById("responseLog");
const welcome = document.getElementById("welcome");
const itemsForm = document.getElementById("itemsForm");
const items = document.getElementById("items");
const itemsTable = document.getElementById("itemsTable");
var viewmodel = null;

// updates the UI post login/token acquisition
function updateUI() {

    if (myMSALObj.account != null) {
        const userName = myMSALObj.getAccount().name;
        logMessage("User '" + userName + "' logged-in");
        signInButton.classList.add('d-none');
        signOutButton.classList.remove('d-none');    
        // greet the user - specifying login
        label.innerText = "Hello " + userName;    
        editProfileButton.classList.remove('d-none');   
        welcome.classList.add('d-none');
        itemsForm.classList.remove('d-none');
        refreshItems(); 
    }
    else {
        logMessage("No user logged in");
        signInButton.classList.remove('d-none');
        signOutButton.classList.add('d-none');            
        label.innerText = "Sign-in with Microsoft Azure AD B2C";    
        editProfileButton.classList.add('d-none'); 
        welcome.classList.remove('d-none');
        itemsForm.classList.add('d-none');
    }
}

// debug helper
function logMessage(s) {
    responseLog.appendChild(document.createTextNode('\n' + s + '\n'));
}

$(function() {
    $('#signIn').click(signIn);
    $('#signOut').click(logout);
    $('#editProfileButton').click(editProfile);
    $('#addItem').click(function() {
        viewmodel.addItem(document.getElementById('txtNewItem').value);
    });

    refreshItems();
    updateUI();
});
