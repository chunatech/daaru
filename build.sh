#!/bin/bash
[ -z "${1}" ] && echo "You must specify a build target." && exit 1

OUTDIR=./build/release/$1

echo "Removing old build files at: ${OUTDIR}"

rm $OUTDIR/cweed
rm $OUTDIR/cweed.pdb
rm -rf $OUTDIR/fsi_standalone/
rm -rf cweed/bin
rm -rf fsi_standalone/bin
rm -rf cweed/obj
rm -rf fsi_standalone/obj

dotnet publish ./cweed/cweed.fsproj -c Release -r $1 -o $OUTDIR --self-contained && \
dotnet publish ./fsi_standalone/fsi_standalone.fsproj -c Release -r $1 -o $OUTDIR/fsi_standalone --self-contained && \
for d in config logs results scripts staging drivers libs; do
    [ ! -d $OUTDIR/$d ] && mkdir $OUTDIR/$d && echo "Created dir ${OUTDIR}/${d}"
done

cp -prnv external_resources/default_drivers/* $OUTDIR/drivers/
cp -prnv external_resources/default_scripts/* $OUTDIR/scripts/
cp -prnv external_resources/default_libs/* $OUTDIR/libs/