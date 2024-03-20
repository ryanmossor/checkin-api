`appsettings.Development.json` contains the following block:

```json
"Kestrel": {
    "Endpoints": {
        "Http": {
            "Url": "http://0.0.0.0:5000"
        },
        "Https": {
            "Url": "https://0.0.0.0:5001"
        }
    }
}
```

However, `appsettings.json` does NOT contain this configuration. Having this in the development settings allows for the API to be reached over the local network. Placing this config in the main `appsettings.json` would also allow for this, but it would then cause issues when deploying the Docker container.

- **TODO:** look into config overrides? maybe keep this block in `appsettings.json` but override it with a more important config elsewhere.