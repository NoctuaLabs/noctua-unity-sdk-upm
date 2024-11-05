﻿# Contribution Guidelines for Noctua SDK Developers

## Manual Release Guide

### Release

1. Ensure you are on the `main` branch.
2. Pull the latest changes:
  ```sh
  git pull
  ```
3. Update the version in `package.json` and `Runtime/AssemblyInfo.cs` if not already done.
4. If the native SDK is updated, also update the version in `Editor/NativePluginDependencies.xml`.

  ```xml
  <dependencies>
    <androidPackages>
        <androidPackage spec="com.noctuagames.sdk:noctua-android-sdk:0.3.9" />
        <androidPackage spec="com.android.billingclient:billing:7.0.0" />
        <androidPackage spec="com.google.guava:guava:31.1-android" />
    </androidPackages>
    <iosPods>
        <iosPod name="NoctuaSDK" version="~> 0.3.0" minTargetSdk="14.0" addToAllTargets="false" />
        <iosPod name="NoctuaSDK/Adjust" version="~> 0.3.0" minTargetSdk="14.0" addToAllTargets="false" />
        <iosPod name="NoctuaSDK/FirebaseAnalytics" version="~> 0.3.0" minTargetSdk="14.0" addToAllTargets="false" />
        <iosPod name="NoctuaSDK/FacebookSDK" version="~> 0.3.0" minTargetSdk="14.0" addToAllTargets="false" />
    </iosPods>
  </dependencies>
  ```

5. Generate the `CHANGELOG.md` manually.
6. Commit and tag the release:
  ```sh
  git add package.json Runtime/AssemblyInfo.cs CHANGELOG.md Editor/NativePluginDependencies.xml
  git commit -m "Release NEW_VERSION_TAG"
  git tag -a NEW_VERSION_TAG -m "Release NEW_VERSION_TAG"
  git push origin main --follow-tags -o ci.skip
  ```

### Publish

1. Install GitHub CLI:
  ```sh
  curl -sS https://webi.sh/gh | sh
  ```
2. Authenticate with GitHub:
  ```sh
  echo $GITHUB_ACCESS_TOKEN | gh auth login --with-token
  ```
3. Create a GitHub release:
  ```sh
  NEW_VERSION_TAG="$(jq -r '.version' package.json)"
  gh release create $NEW_VERSION_TAG --title $NEW_VERSION_TAG --notes "Release notes here"
  ```
