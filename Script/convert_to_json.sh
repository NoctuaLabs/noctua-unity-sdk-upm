#!/bin/bash
#
############################################################################3

# Move to a dedicated buffer for workplace and sort the root fields
cat noctuagg.json | jq '{clientId,copublisher,noctua,adjust,firebase,facebook}' > output.json

# Cleanup the previous object
jq '.facebook.eventMap = {}' output.json | jq '.firebase.eventMap = {}' | jq '.adjust.eventMap = {}' > cleanup.json
rm -rf output

# Prepare placeholder for each platform
cp cleanup.json android.json
cp cleanup.json ios.json
cp cleanup.json windows.json

# Input CSV file
input_file=$1

# Remove existing output file if present
rm -f "$output_file"

# Read the input CSV file line by line
while IFS=',' read -r event_name adjust_android firebase_android facebook_android adjust_ios firebase_ios facebook_ios adjust_windows firebase_windows facebook_windows; do
    # Check if event_name is "event_name", if so, skip processing
    if [[ "$event_name" == "event_name" ]]; then
        continue
    fi
    # Output key-value pairs for Android
    cat android.json  | jq '.adjust.eventMap += {"'$event_name'": "'$adjust_android'"}' > buffer && mv buffer android.json
    cat android.json  | jq '.firebase.eventMap += {"'$event_name'": "'$firebase_android'"}' > buffer && mv buffer android.json
    cat android.json  | jq '.facebook.eventMap += {"'$event_name'": "'$facebook_android'"}' > buffer && mv buffer android.json

    cat ios.json  | jq '.adjust.eventMap += {"'$event_name'": "'$adjust_ios'"}' > buffer && mv buffer ios.json
    cat ios.json  | jq '.firebase.eventMap += {"'$event_name'": "'$firebase_ios'"}' > buffer && mv buffer ios.json
    cat ios.json  | jq '.facebook.eventMap += {"'$event_name'": "'$facebook_ios'"}' > buffer && mv buffer ios.json

    cat windows.json  | jq '.adjust.eventMap += {"'$event_name'": "'$adjust_windows'"}' > buffer && mv buffer windows.json
    cat windows.json  | jq '.firebase.eventMap += {"'$event_name'": "'$firebase_windows'"}' > buffer && mv buffer windows.json
    cat windows.json  | jq '.facebook.eventMap += {"'$event_name'": "'$facebook_windows'"}' > buffer && mv buffer windows.json

done < "$input_file"

mkdir -p output/android
mkdir -p output/ios
mkdir -p output/windows

mv android.json output/android/noctuagg.json
mv ios.json output/ios/noctuagg.json
mv windows.json output/windows/noctuagg.json

# Cleanup
rm -rf buffer
rm -rf cleanup.json
