## Novidades da v0.19

**RESET OTIMIZA · Serviços desnecessários**
- Novo botão **PARAR TODOS**: encerra de uma vez todos os serviços opcionais que estão em execução, sem precisar
  marcar um por um. Mostra a lista antes de confirmar e avisa que impressora, busca, Bluetooth e Xbox/Game Pass
  também serão parados.
- Como nos demais botões, nada é desinstalado: os serviços voltam ao normal no próximo reinício do Windows.

## Novidades da v0.18

**RESET OTIMIZA · Lista completa de programas**
- A lista "PROGRAMAS PARA FECHAR" agora mostra também os apps que rodam **sem janela**, como Armoury Crate,
  ASUS Framework, Steam, Discord, WebView2 e Bun — antes eles eram escondidos e só apareciam no Gerenciador de Tarefas.
- Apps de segundo plano conhecidos ganharam descrição (Armoury Crate, Razer, Logitech, Corsair, MSI, Wallpaper Engine,
  Nahimic, launchers etc.) e os seguros continuam pré-marcados.
- "Dispositivos móveis" (CrossDeviceService/CrossDeviceResume), Widgets e o feed de notícias agora aparecem e são
  pré-selecionados.
- Novo interruptor **MOSTRAR TAMBÉM PROCESSOS DO SISTEMA** para ver o restante da sessão sem os componentes protegidos.
- Botão **MARCAR SEM JANELA** para selecionar de uma vez todos os apps de segundo plano (Armoury Crate, ASUS, Discord,
  Steam, WebView2...), sem incluir ferramentas de desenvolvimento como OpenCode/Bun, Node, Python e Docker.
- Apps que só rodam em segundo plano são encerrados por completo ao aplicar o RESET OTIMIZA; apps com janela continuam
  recebendo o pedido de fechar (como clicar no X), sem forçar.

## Novidades da v0.17

**RESET OTIMIZA · Limpeza do sistema**
- **Esvaziar lixeira**: limpa a lixeira de todas as unidades e mostra quanto espaço foi liberado.
- **Limpar temporários**: apaga o conteúdo de todas as pastas temporárias (de cada usuário e do Windows), o cache de
  atualizações do Windows, a Otimização de Entrega, relatórios de erro e o cache de shaders das GPUs. Arquivos em uso são
  mantidos, então dá para limpar com o PC ligado; o Prefetch é preservado porque acelera a abertura de programas e jogos.
- **Limpar cache DNS**: resolve na hora endereços de servidores/lobby que mudaram.
- **Serviços desnecessários abertos**: lista os serviços opcionais em execução (telemetria, relatórios de erro, fax, registro
  remoto, localização, Xbox/Game Pass, Bluetooth, impressão...) com os seguros já marcados. Encerrar não desinstala nada:
  tudo volta ao reiniciar o Windows.
- **Limpeza completa**: roda lixeira + temporários + DNS + serviços marcados em um clique.

## Novidades da v0.16

**Nova página · RESET FERRAMENTAS › Inicialização**
- Lista os programas que abrem junto com o Windows: chaves Run do registro (usuário, todos os usuários, 32 e 64 bits)
  e pastas Inicializar, com descrição, fabricante e aviso quando o arquivo do programa não existe mais.
- Interruptor para ativar/desativar, igual ao Gerenciador de Tarefas: a entrada continua lá e pode ser religada.
- Botão **REMOVER** com confirmação; o programa continua instalado e tudo pode ser desfeito em **Restaurar removidos**.

**Nova página · RESET FERRAMENTAS › Instalar apps**
- Os essenciais depois de formatar o PC: Steam, Google Chrome, Discord, Lightshot, 7-Zip, WinRAR e VLC.
- Instala pelo winget (instalador de pacotes da Microsoft que já vem no Windows): versão oficial mais recente,
  sem janelas, um app de cada vez, com o resultado de cada um na lista.
- Mostra o que já está instalado; se o winget ainda não existir (PC recém-formatado), um botão abre a Microsoft Store
  para atualizar o "Instalador de Aplicativo".

**Nova categoria · RESET MAKER** (Arduino, ESP32 e conversores USB-serial)
- **Portas COM**: a limpeza de dispositivos fantasmas e de números COM presos saiu de Otimização e ganhou página própria.
- **Drivers seriais**: diagnóstico das placas com CH340/CH341, CP2102/CP210x, FTDI, Prolific PL2303 e Arduino
  (sem driver, código 10, código 43, FTDI falsificado travado em PID 0000), com a orientação de cada caso.
- **REINSTALAR** remove o dispositivo e faz o Windows detectar de novo com o melhor driver.
- Lista as versões de driver guardadas no Windows para cada chip, com **REMOVER** (backup antes): volta para uma versão
  anterior quando a nova não funciona com clones, ou desinstala de vez. **Restaurar removidos** desfaz.
- **Drivers incluídos no app**: CH340 4.0, CP210x 11.6, FTDI 2.12.36.20 e Arduino, oficiais e assinados, instalam sem internet
  (ideal logo depois de formatar), com a placa conectada ou não. Também abre a página oficial de cada fabricante.
- **Arduino IDE**: instalar/atualizar pelo winget, fechar programas que prendem a porta COM e limpar o cache de índices
  quando a IDE trava em "Downloading index".

**Novo benchmark · Ray tracing (GPU)**
- Teste em Direct3D 12 com DXR 1.1 que usa os núcleos de ray tracing (RTX 20+, Radeon RX 6000+, Intel Arc): 4 mil objetos,
  reflexos de reflexos, sombras suaves, oclusão de ambiente e iluminação indireta, com a cena refeita a cada frame.
- Feito para comparar com confiança: aquecimento sem medir, 3 passadas iguais, nota pela mediana e variação entre passadas
  (selo de "resultado confiável" abaixo de 3%); tempo de frame medido pelo relógio da própria GPU.
- Presets Normal, Alta e Extrema em 1080p, 1440p e 4K; histórico comparando só resultados do mesmo preset e resolução.
- Referência: RX 9070, Alta 1440p = 878 pts (87,8 FPS), variação 0,5%.

**Overlay personalizável**
- Na aba Overlay, escolha o que aparece: FPS, frametime, 1% low, gráfico; GPU (uso, temperatura, hotspot, clock do núcleo,
  clock da memória, consumo, tensão, ventoinha, limitador NVIDIA); VRAM (usada, %, temperatura); CPU (uso, núcleo mais
  ocupado, temperatura, clock médio, clock máximo, consumo); RAM (GB, %); nome do jogo e hora. Atalhos SÓ FPS, PADRÃO e TUDO.
- Cada componente vira uma linha só com os itens escolhidos; a escolha fica salva e vale na hora.

**RESET JOGOS**
- Novo atalho em Extras: **-allow_third_party_software** (CS2), que libera captura de jogo do OBS, ReShade e overlays
  bloqueados pelo modo confiável; o aviso sobre o Fator de Confiança aparece ao passar o mouse.

**Abertura mais rápida**
- A janela aparece em ~0,5 s (antes ~4,6 s). Informações do sistema, sensores e placa de vídeo carregam em paralelo,
  em segundo plano, e cada página é preenchida quando a sua parte fica pronta.
- O aviso "sem sensor da CPU" não aparece mais por engano enquanto o hardware ainda está sendo detectado.

**Correção · Overlay de jogos**
- O overlay agora fica por cima de jogos em janela sem borda que também se marcam como "sempre no topo"
  (ex.: GTA V e Cyberpunk 2077), inclusive depois de Alt+Tab.
- Aparece no monitor onde o jogo está, não só no monitor principal.
