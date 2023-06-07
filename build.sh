#!/bin/bash
OUTDIR=./build/release/linux-x64

echo "Removing old build files at: ${OUTDIR}"

rm $OUTDIR/cweed
rm $OUTDIR/cweed.pdb
rm $OUTDIR/fsi_standalone
rm $OUTDIR/fsi_standalone.pdb

dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishTrimmed=true /p:PublishSingleFile=true -o $OUTDIR

for d in config logs results scripts staging drivers
do
    [ ! -d $OUTDIR/$d ] && mkdir $OUTDIR/$d && echo "Created dir ${OUTDIR}/${d}"
done