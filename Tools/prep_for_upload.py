
import sys 
import os
import shutil

prep_roots = ["Data", "Textures", "Models", "Audio", "LICENSE", "README.md", "thumb.png"]


copy_extns = [".mwm", ".cs", ".sbc", ".dds", ".wav"]

def copy_path(path: str, readin: [str]):
    exten = os.path.splitext(path)[1]
    if os.path.isdir(path):
        for p in os.listdir(path):
            copy_path(os.path.join(path, p), readin)
    elif exten in copy_extns:
        readin.append(path)


def copy_to(prefix: str, rmprefix: str, files: [str]):
    for file in files:
        dst = os.path.join(prefix, file[len(rmprefix) + 1:] if file.startswith(rmprefix) else file)
        print("{} -> {}".format(file, dst))
        parent_dir = os.path.dirname(dst)
        if parent_dir and not os.path.exists(parent_dir):
            print("makedir: {}".format(parent_dir))
            os.makedirs(parent_dir)
        
        shutil.copyfile(file, dst)

if __name__ == '__main__':
    infile = sys.argv[1]
    outfile = sys.argv[2]
    outfile = outfile.replace('"', '')
    
    to_copy = []
    for file in os.listdir(infile):
        if (os.path.basename(file) + os.path.splitext(file)[1]) in prep_roots:
            copy_path(os.path.join(infile, file), to_copy)
    copy_to(outfile, infile, to_copy)
    
    
    
    
    
	
	

