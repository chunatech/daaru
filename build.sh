#!/bin/bash
[ -z "${1}" ] && echo "You must specify a build target." && exit 1

OUTDIR=./build/release/$1/daaru

echo "Removing old build files at: ${OUTDIR}"

rm $OUTDIR/cw*
rm -rf $OUTDIR/fsi_standalone/
rm -rf src/*/bin
rm -rf src/*/obj

dotnet publish ./src/daaru/daaru.fsproj -c Release -r $1 -o $OUTDIR --self-contained && \
dotnet publish ./src/fsi_standalone/fsi_standalone.fsproj -c Release -r $1 -o $OUTDIR/fsi_standalone --self-contained && \

for d in config logs results scripts staging drivers libs screenshots templates; do
    [ ! -d $OUTDIR/$d ] && mkdir $OUTDIR/$d && echo "Created dir ${OUTDIR}/${d}"
done

cp -prnv external_resources/default_drivers/$1/* $OUTDIR/drivers/
cp -prnv external_resources/default_scripts/* $OUTDIR/scripts/
cp -prnv external_resources/default_libs/* $OUTDIR/libs/
cp -prnv external_resources/default_config/* $OUTDIR/config/
cp -prnv external_resources/default_templates/* $OUTDIR/templates/
cp -prnv external_resources/default_results_processing/* $OUTDIR/results/