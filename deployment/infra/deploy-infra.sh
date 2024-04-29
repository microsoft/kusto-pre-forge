#!/bin/bash

##########################################################################
##  Deploys Azure infrastructure

rg=$1
registryName=$2

echo "Resource group:  $rg"
echo "registryName:  $registryName"
echo "Current directory:  $(pwd)"

echo
echo "Deploying ARM template"

az deployment group create -n "deploy-$(uuidgen)" -g $rg \
    --template-file main.bicep \
    --parameters registryName=$registryName
