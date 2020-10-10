// The current application coordinates were pre-registered in a B2C tenant.
const apiConfig = {
  b2cScopes: [
    "https://mytenant.onmicrosoft.com/todoapi/read",
    "https://mytenant.onmicrosoft.com/todoapi/write"
  ],
  webApi: "https://my-todolist-api.azurewebsites.net/api/tasks"
};