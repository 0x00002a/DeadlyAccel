import sys
import os
import subprocess as sp
from multiprocessing import Pool, cpu_count

def to_png(infile: str):
    print("{} -> {}".format(infile, os.path.splitext(infile)[0] + ".dds"))
    sp.run(["texconv.exe", "-nologo", "-y", "-f", "BC7_UNORM", "-pmalpha", "-if", "LINEAR", "-l", infile])
    
    
def to_png_mp(infiles: [str]):
    job_pool = Pool(cpu_count())
    job_pool.map(to_png, infiles)

if __name__ == '__main__':
    infile = "."
    if len(sys.argv) > 1:
        infile = sys.argv[1]
    
    conv_files = []
    if os.path.isdir(infile):
        conv_files = [f for f in os.listdir(infile) if os.path.splitext(f)[1] == ".png"]
    elif len(sys.argv) > 1:
        conv_files = sys.argv[1:]
    
    to_png_mp(conv_files)

