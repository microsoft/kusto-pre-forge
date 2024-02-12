#!/bin/bash

##########################################################################
##  Deploys Azure infrastructure

dos2unix ../../code/IntegrationTests/TestCaseConfig.json
dos2unix main.parameters.template.json

rg=$1
testIdentityId=$2
testIdentityObjectId=$3
testCases=$(<../../code/IntegrationTests/TestCaseConfig.json)

echo "Resource group:  $rg"
echo "testIdentityId:  $testIdentityId"
echo "testIdentityObjectId:  $testIdentityObjectId"
echo "Current directory:  $(pwd)"

echo "Test cases:"
echo "$testCases"

#   Find and replace the value
awk -v value="$testCases" '{gsub(/<VALUE>/, value)}1' main.parameters.template.json > main.parameters.json

echo "Parameters:"
cat main.parameters.json

echo
echo "Deploying ARM template"

az deployment group create -n "deploy-$(uuidgen)" -g $rg \
    --template-file main.bicep \
    --parameters @main.parameters.json \
    --parameters testIdentityId=$testIdentityId testIdentityObjectId=$testIdentityObjectId 
