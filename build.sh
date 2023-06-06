#!/bin/bash
OUTDIR=./build/release/linux-x64

rm -rf $OUTDIR

while [ -d $OUTDIR ]
do
    sleep 3
done

dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishTrimmed=true /p:PublishSingleFile=true -o $OUTDIR

[ ! -d $OUTDIR/bin ] && mkdir $OUTDIR/bin

for f in $(ls $OUTDIR | grep -v bin)
do
    mv $OUTDIR/$f $OUTDIR/bin/
done

for d in config logs results scripts staging
do
    [ ! -d ./bin/cweed ] && mkdir $OUTDIR/$d
done