#!/bin/bash

##########################################################################
##  Deploys Azure infrastructure

rg=$1
testIdentityId=$2
testIdentityObjectId=$3

echo "Resource group:  $rg"
echo "testIdentityId:  $testIdentityId"
echo "testIdentityObjectId:  $testIdentityObjectId"
echo "Current directory:  $(pwd)"

echo "Test cases:"
echo "$testCases"

#   Create parameter file
cat main.parameters.template.json ../../code/IntegrationTests/TestCaseConfig.json <(echo "}}}") > main.parameters.json

echo "Parameters:"
cat main.parameters.json

echo
echo "Deploying ARM template"

az deployment group create -n "deploy-$(uuidgen)" -g $rg \
    --template-file main.bicep \
    --parameters @main.parameters.json \
    --parameters testIdentityId=$testIdentityId testIdentityObjectId=$testIdentityObjectId 
