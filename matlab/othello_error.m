dotnetenv("core");
library = NET.addAssembly('C:\Users\zyand\source\repos\dojadon\OthelloAI\bin\Release\net6.0\OthelloAI.dll');

import OthelloAI.*

n = 10000;
depths = (0:2)*2;
%% 

net_array = Tester.TestError(Program.WEIGHT, depths, n);
aa = cell2mat(cell(net_array));
a = reshape(aa, 64, [], n);
b = squeeze(mean(a, 3));
%% 

hold on
for d = depths
    plot(d:(d+43), b(13:56, k));
    % plot(b(13:56, k));
end
hold off

% ylim([400, 1000]);