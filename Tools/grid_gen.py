from PIL import Image, ImageTk
import tkinter as tk 
from tkinter import ttk
import sys 
import os

resolution = 30
grid_colour_rgb = (0, 255, 0)
display_scaling = 0.5

def generate_grid(img):
    pixmap = img.load()

    width_px = img.size[0]
    height_px = img.size[1]

    def is_grid_px(x, y):
        return x % resolution == 0 or y % resolution == 0

    for xpix in range(img.size[0]):
        for ypix in range(img.size[1]):
            if is_grid_px(xpix, ypix):
                pixmap[xpix, ypix] = grid_colour_rgb

    return img
   

def run_gui(img):
    
    root = tk.Tk()
    
    img = img.resize((int(img.size[0] * display_scaling), int(img.size[1] * display_scaling)), resample=Image.BILINEAR)
    pict = ImageTk.PhotoImage(img)
    
    
    status = tk.Frame()
    xpos_lbl = ttk.Label(status)
    ypos_lbl = ttk.Label(status)
    
    ttk.Label(status, text="(x: ").pack(side=tk.LEFT)
    xpos_lbl.pack(side=tk.LEFT)
    ttk.Label(status, text=" , ").pack(side=tk.LEFT)
    ypos_lbl.pack(side=tk.LEFT)
    ttk.Label(status, text="y: )").pack(side=tk.LEFT)
    
    
  
    can = tk.Canvas(height=img.size[1], width=img.size[0])
    can.pack(expand=False)
    can.create_image(0, 0, image=pict, anchor="nw")
    
    def pos_changed(x, y):
        x_translated = -1 + x * (2 / img.size[0])
        y_translated = 1 - y * (2 / img.size[1])
        
        xpos_lbl.config(text=str(round(x_translated, 3)))
        ypos_lbl.config(text=str(round(y_translated, 3)))
        
  
    
    
    can.bind("<Motion>", lambda ev: pos_changed(ev.x, ev.y))
    
    status.pack(expand=True)
    root.mainloop()

if __name__ == '__main__':
    infile = sys.argv[1]
    
    img = Image.open(infile)
    if len(sys.argv) < 3 or sys.argv[2] != "--no-grid":
        img = generate_grid(img)
        
    run_gui(img)

