import networkx as nx
import matplotlib.pyplot as plt
import matplotlib.patches as patches
import math
import sys

plt.figure(figsize=(20, 12), dpi=60) 

# NetworkX is gay. Extracted from nx.draw(), allows always maximize plot
ax = plt.gcf().add_axes((0, 0, 1, 1))


RENDER_LEFT = True


g = nx.Graph()

def process_params(aka, discord):
    counter = 1
    if aka:
        aka = '\n\n' + aka
        counter = 2
    if discord:
        discord = '\n\n'*counter + discord
    return aka, discord

def add_single(a, aka='', discord=''):
    aka, discord = process_params(aka, discord)
    g.add_node(a, aka=aka, discord=discord)

def add_unknown(a, aka='', discord=''):
    add_single(a, aka, discord)
    g.add_edge('Arkyo', a, color='white', weight=5)

def add_fd(a, b, aka='', discord='', color='red'):
    aka, discord = process_params(aka, discord)
    g.add_node(b, aka=aka, discord=discord)
    g.add_edge(a, b, color=color, weight=3)

def add_alt(a, b):
    g.add_node(b, aka='', discord='')
    g.add_edge(a, b, color='blue', weight=2)

if RENDER_LEFT:
    def add_left(a, b, aka='', discord=''):
        add_fd(a, b, aka=aka, discord=discord, color='black')
else:
    def add_left(a, b):
        pass


add_single('Arkyo', aka='Dim', discord='ねねSniffer')
add_unknown('kelvin')
add_unknown('早点睡')
add_fd('Arkyo', '有錢你就是神', aka='Billy', discord='changbilly')
add_alt('有錢你就是神', '香蕉爬來爬去')
add_fd('錢就是王法', 'Meow')
add_fd('有錢你就是神', '錢就是王法')
add_alt('有錢你就是神', '看你妹看')
add_alt('有錢你就是神', '☢️沒錢人的遊戲')
add_alt('有錢你就是神', '哥的名字就是屌')
add_fd('Arkyo', 'Issac', discord='アルトリア')
add_fd('Arkyo', 'Legend of Birds', aka='Terry', discord='TerryTNT')
add_fd('Arkyo', 'Slamdunk', aka='Sam', discord='Atlas')
add_fd('Arkyo', 'bazil', discord='meraki')
add_fd('bazil', 'Sherlock', discord='Cali-Anthenics')
add_fd('有錢你就是神', '雪兒')
add_alt('有錢你就是神', '沒錢人的遊戲🌚')
add_alt('有錢你就是神', 'timtim')
add_fd('錢就是王法', 'justin')
add_fd('Arkyo', 'DragonWarrior', aka='Kingsman', discord='Kingsman')
add_fd('DragonWarrior', 'Sigma 30')
add_fd('Arkyo', 'Arisaka', aka='Lok', discord='Ahlok')
add_fd('Arisaka', '過期方包', aka='Bread', discord='Breeaad')
add_fd('Arkyo', 'WT', discord='MF')
add_fd('Issac', '金剛大好', aka='Mickey', discord='Reiin')
add_fd('Arkyo', 'FrenchFriezy', aka='Matt', discord='FrenchFriezy')
add_alt('DragonWarrior', 'dont attack me')
add_alt('Legend of Birds', 'Mohoidae')
add_alt('bazil', 'meraki')
add_alt('DragonWarrior', 'ho ho ho')
add_fd('金剛大好', 'GinYuri', discord='GinYuri')
add_fd('bazil', 'boss')
add_alt('Sigma 30', 'Sigma 11')
add_alt('Sigma 30', 'ynw slime')
add_fd('Issac', 'TooLazy')
add_fd('Issac', 'dogge', discord='Shrek')
add_fd('bazil', 'Kratos')
add_alt('金剛大好', 'Mikatsuki')
add_fd('dogge', 'grace')
add_alt('Arisaka', 'Chino')
add_alt('過期方包', 'Bread')
add_left('Sigma 30', 'Epic')
add_fd('Arkyo', 'Sabeel', discord='Sabeel')
add_fd('有錢你就是神', '加油')
add_fd('Sherlock', 'Stellar')
add_fd('Arisaka', 'Lukas', discord='Zhao')
add_alt('TooLazy', 'wendaDolken')


layout = nx.kamada_kawai_layout(g)
nx.draw_networkx(g, pos=layout, node_size=250, edge_color=[g[u][v]['color'] for u,v in g.edges()], ax=ax)
nx.draw_networkx_labels(g, pos=layout)
nx.draw_networkx_labels(g, pos=layout, labels={k: v['aka'] for k,v in g.nodes.items()}, font_color='green', font_weight='heavy')
nx.draw_networkx_labels(g, pos=layout, labels={k: v['discord'] for k,v in g.nodes.items()}, font_color='#9100ff', font_weight='heavy')
plt.rcParams['font.sans-serif']= 'WenQuanYi Micro Hei', 'Noto Color Emoji', 'Microsoft JhengHei', 'Segoe UI Emoji'
r_patch = patches.Patch(color='r', label='Friend')
b_patch = patches.Patch(color='b', label='Alt')
bl_patch = patches.Patch(color='black', label='Left')
g_patch = patches.Patch(color='g', label='Nickname')
p_patch = patches.Patch(color='purple', label='Discord')
plt.legend(handles=(r_patch, b_patch, bl_patch, g_patch, p_patch))
if len(sys.argv) == 2 and sys.argv[1] == 'save':
    plt.savefig('Figure_1.png')
else:
    plt.show()