import sqlite3
import os
import sys
import string
from pprint import pprint

assetCount = 0
typeCount = {}

lastAssetType = None
lastAssetLoc = 0
assetSizes = {}

matchingSizes = {}

def dictAdd(mdict, key, add):
    if key in mdict:
        mdict[key] = mdict[key] + add
    else:
        mdict[key] = add

if __name__ == "__main__":
    baseDir = sys.argv[1]
    for i in range(4096):
        hexStr = "%03x" % i
        
        #open the corresponding sqlite file
        con = None
        lastAssetType = None
        lastAssetLoc = 0
        try:
            dfile  = os.path.join(baseDir, hexStr, "globals.idx")
            print "Opening " + dfile
            con = sqlite3.connect(dfile)
            cur = con.cursor()    
            cur.execute('SELECT * FROM VFSDataIndex')

            #asset_id, position, type, created_on, deleted
            while True:
                row = cur.fetchone()
                if row == None:
                    break
                    
                assetCount += 1
                
                if lastAssetType != None:
                    #insert size
                    lastSize = row[1] - lastAssetLoc
                    dictAdd(assetSizes, lastAssetType, lastSize)
        
                lastAssetLoc = row[1]
                lastAssetType = row[2]
                
                dictAdd(typeCount, row[2], 1)
    
    
        except sqlite3.Error, e:
            print "Error %s:" % e
            sys.exit(1)
    
        finally:
            if con:
                con.close()

    print "Summary"
    print "Asset Count Total: " + str(assetCount)
    print "Type count: "
    pprint(typeCount)
    print "Sizes: "
    pprint(assetSizes)
    print "Total Size: "
    totalSz = sum(assetSizes.values())
    print totalSz
    print "Percentage of total size: "
    for key, value in assetSizes.iteritems():
        print "  " + str(key) + ": " + ("%.3f" % float((float(value) / float(totalSz)) * 100.0)) + "%"
    print
    print "Avg size per type: "
    for key, value in assetSizes.iteritems():
        tcount = typeCount[key]
        print "  " + str(key) + ": " + str(value / tcount)


