# NuGet Publishing

This project uses [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) via OIDC.
Pushing a `v*` tag triggers `.github/workflows/publish.yml` which builds, packs, and pushes to NuGet.org.

## To publish a new version

1. Bump `<Version>` in `src/ApproximateSpanMatching/ApproximateSpanMatching.csproj`.
2. Commit the version bump.
3. Tag with `v<version>` matching the `.csproj` version.
4. Push both:

```
git add src/ApproximateSpanMatching/ApproximateSpanMatching.csproj
git commit -m "Bump version to X.Y.Z"
git tag vX.Y.Z
git push origin
git push origin vX.Y.Z
```

GitHub Actions will build, pack, and publish to NuGet.org — no API key management needed.
