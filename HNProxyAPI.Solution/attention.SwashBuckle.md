> **IMPORTANT:** Please stick to **Swashbuckle.AspNetCore version 6.2.2**. 
> Updating to the latest NuGet version causes a breaking change where the generated `swagger.json` uses an OpenAPI version (`3.0.4`) that is not yet supported by the Swagger UI frontend, resulting in a rendering error.

> **Note:** After testing multiple versions, we confirmed that **v7.3.2** is the most recent compatible release. 
> It resolves both the Swagger UI rendering error ("invalid version field") and the runtime `MissingMethodException` caused by `Microsoft.OpenApi` mismatches found in other versions.

## ⚠️ Known Issues: Swagger UI Versioning

If you encounter the error **"Unable to render this definition"** or **"The provided definition does not specify a valid version field"** when running the API:

* **Cause:** Updating `Swashbuckle.AspNetCore` to the latest version via NuGet may generate an `openapi: 3.0.4` specification, which is currently incompatible with the Swagger UI validator.
* **Solution:** Downgrade the `Swashbuckle.AspNetCore` package to version **6.2.2**. This version generates a compatible JSON schema.

