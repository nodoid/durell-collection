import sys
sys.path.insert(0, sys.argv[0].rsplit('/',1)[0])
from tap import parse
TOK = ["END","EDIT","STORE","RECALL","TRON","TROFF","POP","PLOT","PULL","LORES","DOKE","REPEAT","UNTIL","FOR","LLIST","LPRINT","NEXT","DATA","INPUT","DIM","CLS","READ","LET","GOTO","RUN","IF","RESTORE","GOSUB","RETURN","REM","HIMEM","GRAB","RELEASE","TEXT","HIRES","SHOOT","EXPLODE","ZAP","PING","SOUND","MUSIC","PLAY","CURSET","CURMOV","DRAW","CIRCLE","PATTERN","FILL","CHAR","PAPER","INK","STOP","ON","WAIT","CLOAD","CSAVE","DEF","POKE","PRINT","CONT","LIST","CLEAR","GET","CALL","!","NEW","TAB(","TO","FN","SPC(","@","AUTO","ELSE","THEN","NOT","STEP","+","-","*","/","^","AND","OR",">","=","<","SGN","INT","ABS","USR","FRE","POS","HEX$","&","SQR","RND","LN","EXP","COS","SIN","TAN","ATN","PEEK","DEEK","LOG","LEN","STR$","VAL","ASC","CHR$","PI","TRUE","FALSE","KEY$","SCRN","POINT","LEFT$","RIGHT$","MID$"]
def listing(data, base):
    p=0; out=[]
    while p+4 <= len(data):
        nxt=data[p]|data[p+1]<<8
        if nxt==0: break
        ln=data[p+2]|data[p+3]<<8; p+=4; s=''
        while p<len(data) and data[p]!=0:
            c=data[p]; s+= (TOK[c-0x80] if 0x80<=c-0 and c-0x80<len(TOK) else (chr(c) if 32<=c<127 else '{%02X}'%c)); p+=1
        p+=1; out.append(f"{ln} {s}")
    return out
if __name__=='__main__':
    for b in parse(open(sys.argv[1],'rb').read()):
        if b['type']==0:
            print('##',b['name']); print('\n'.join(listing(b['data'],b['start'])))
