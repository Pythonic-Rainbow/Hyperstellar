import matplotlib.font_manager
for font in sorted(set([f.name for f in matplotlib.font_manager.fontManager.ttflist])):
    print(font)