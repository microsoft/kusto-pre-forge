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

#   Infer test cases
testCases=$(sed 's/"/'\''/g; s/,//g' "../../code/IntegrationTests/TestCaseConfig.json")
echo "Test cases:"
echo "$testCases"

echo
echo "Deploying ARM template"

az deployment group create -n "deploy-$(uuidgen)" -g $rg \
    --template-file main.bicep \
    --parameters testIdentityId=$testIdentityId testIdentityObjectId=$testIdentityObjectId \
    testCases=$testCases
