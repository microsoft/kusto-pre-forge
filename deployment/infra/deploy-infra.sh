#!/bin/bash

##########################################################################
##  Deploys Azure infrastructure

rg=$1
testIdentityId=$2
testIdentityObjectId=$3

echo "Resource group:  $rg"
echo "testIdentityId:  $testIdentityId"
echo "testIdentityObjetId:  $testIdentityObjetId"
echo "Current directory:  $(pwd)"

echo
echo "Deploying ARM template"

az deployment group create -n "deploy-$(uuidgen)" -g $rg \
    --template-file main.bicep \
    --parameters testIdentityId=$testIdentityId testIdentityObjetId=$testIdentityObjetId
