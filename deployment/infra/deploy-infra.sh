#!/bin/bash

##########################################################################
##  Deploys Azure infrastructure

rg=$1
kustoIngestUri=$2
kustoDb=$3
kustoTable=$4

echo "Resource group:  $rg"
echo "kustoIngestUri:  $kustoIngestUri"
echo "kustoDb:  $kustoDb"
echo "kustoTable:  $kustoTable"
echo "Current directory:  $(pwd)"

echo
echo "Deploying ARM template"

az deployment group create -n "deploy-$(uuidgen)" -g $rg \
    --template-file main.bicep