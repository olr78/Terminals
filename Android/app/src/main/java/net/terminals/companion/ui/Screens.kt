package net.terminals.companion.ui

import android.content.Context
import android.net.Uri
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Clear
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.launch
import net.terminals.companion.R
import net.terminals.companion.core.Favorite
import net.terminals.companion.launch.ConnectionLauncher

@Composable
fun TerminalsTheme(content: @Composable () -> Unit) {
    val dark = isSystemInDarkTheme()
    val colors = if (dark)
        darkColorScheme(primary = Color(0xFF8AB4F8), secondary = Color(0xFF9CC9A6))
    else
        lightColorScheme(primary = Color(0xFF1F5FAD), secondary = Color(0xFF2E7D4F))
    MaterialTheme(colorScheme = colors, content = content)
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TerminalsApp(viewModel: VaultViewModel) {
    val state by viewModel.state.collectAsState()
    val message by viewModel.message.collectAsState()
    val snackbar = remember { SnackbarHostState() }
    val context = LocalContext.current
    val picker = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri: Uri? ->
        if (uri != null)
            viewModel.import(uri)
    }
    val openPicker = { picker.launch(arrayOf("text/xml", "application/xml", "text/plain", "application/octet-stream", "*/*")) }
    var menuOpen by remember { mutableStateOf(false) }
    var confirmDelete by remember { mutableStateOf(false) }

    LaunchedEffect(message) {
        message?.let {
            snackbar.showSnackbar(context.getString(it))
            viewModel.messageShown()
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(stringResource(R.string.app_name)) },
                actions = {
                    if (state is VaultState.Unlocked) {
                        IconButton(onClick = viewModel::lock) {
                            Icon(Icons.Filled.Lock, stringResource(R.string.lock))
                        }
                    }
                    if (state !is VaultState.Empty) {
                        IconButton(onClick = { menuOpen = true }) {
                            Icon(Icons.Filled.MoreVert, stringResource(R.string.menu))
                        }
                        DropdownMenu(expanded = menuOpen, onDismissRequest = { menuOpen = false }) {
                            DropdownMenuItem(text = { Text(stringResource(R.string.import_new)) },
                                onClick = { menuOpen = false; openPicker() })
                            DropdownMenuItem(text = { Text(stringResource(R.string.delete_export)) },
                                onClick = { menuOpen = false; confirmDelete = true })
                        }
                    }
                })
        },
        snackbarHost = { SnackbarHost(snackbar) },
    ) { padding ->
        Box(Modifier.padding(padding).fillMaxSize()) {
            when (val current = state) {
                VaultState.Empty -> EmptyScreen(openPicker)
                is VaultState.Locked -> UnlockScreen(current, viewModel::unlock)
                is VaultState.Unlocked -> FavoritesScreen(current.favorites, snackbar)
            }
        }
    }

    if (confirmDelete) {
        AlertDialog(
            onDismissRequest = { confirmDelete = false },
            title = { Text(stringResource(R.string.delete_export)) },
            text = { Text(stringResource(R.string.delete_export_question)) },
            confirmButton = {
                TextButton(onClick = { confirmDelete = false; viewModel.deleteExport() }) {
                    Text(stringResource(R.string.delete))
                }
            },
            dismissButton = {
                TextButton(onClick = { confirmDelete = false }) { Text(stringResource(R.string.cancel)) }
            })
    }
}

@Composable
private fun EmptyScreen(onImport: () -> Unit) {
    Column(
        Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(24.dp),
        verticalArrangement = Arrangement.Center,
    ) {
        Text(stringResource(R.string.empty_title), style = MaterialTheme.typography.headlineSmall)
        Spacer(Modifier.height(16.dp))
        Text(stringResource(R.string.empty_steps), style = MaterialTheme.typography.bodyLarge)
        Spacer(Modifier.height(24.dp))
        Button(onClick = onImport, modifier = Modifier.fillMaxWidth()) { Text(stringResource(R.string.import_file)) }
    }
}

@Composable
private fun UnlockScreen(state: VaultState.Locked, onUnlock: (CharArray) -> Unit) {
    var password by rememberSaveable { mutableStateOf("") }
    val focus = remember { FocusRequester() }
    val enabled = !state.working && state.waitSeconds == 0
    val submit = {
        if (enabled && password.isNotEmpty()) {
            onUnlock(password.toCharArray())
            password = ""
        }
    }

    LaunchedEffect(Unit) { focus.requestFocus() }

    Column(
        Modifier.fillMaxSize().imePadding().padding(24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Icon(Icons.Filled.Lock, null, Modifier.size(48.dp), tint = MaterialTheme.colorScheme.primary)
        Spacer(Modifier.height(16.dp))
        Text(stringResource(R.string.unlock_title), style = MaterialTheme.typography.titleLarge)
        Spacer(Modifier.height(24.dp))
        OutlinedTextField(
            value = password,
            onValueChange = { if (enabled) password = it },
            label = { Text(stringResource(R.string.export_password)) },
            singleLine = true,
            readOnly = !enabled,
            visualTransformation = PasswordVisualTransformation(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password, imeAction = ImeAction.Done),
            keyboardActions = KeyboardActions(onDone = { submit() }),
            modifier = Modifier.fillMaxWidth().widthIn(max = 480.dp).focusRequester(focus),
        )
        Spacer(Modifier.height(8.dp))
        val error = when {
            state.waitSeconds > 0 -> stringResource(R.string.too_many_attempts, state.waitSeconds)
            state.errorId != null -> stringResource(state.errorId)
            else -> null
        }
        Text(error ?: "", color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodyMedium)
        Spacer(Modifier.height(16.dp))
        Button(onClick = submit, enabled = enabled && password.isNotEmpty(), modifier = Modifier.fillMaxWidth()) {
            if (state.working)
                CircularProgressIndicator(Modifier.size(20.dp), strokeWidth = 2.dp)
            else
                Text(stringResource(R.string.unlock))
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun FavoritesScreen(favorites: List<Favorite>, snackbar: SnackbarHostState) {
    var query by rememberSaveable { mutableStateOf("") }
    var group by rememberSaveable { mutableStateOf<String?>(null) }
    var selected by remember { mutableStateOf<Favorite?>(null) }
    val groups = remember(favorites) { favorites.flatMap { it.groups }.distinct().sortedWith(String.CASE_INSENSITIVE_ORDER) }
    val visible = remember(favorites, query, group) {
        favorites.filter { (group == null || group in it.groups) && it.matches(query) }
    }

    Column(Modifier.fillMaxSize()) {
        OutlinedTextField(
            value = query,
            onValueChange = { query = it },
            placeholder = { Text(stringResource(R.string.search)) },
            leadingIcon = { Icon(Icons.Filled.Search, null) },
            trailingIcon = {
                if (query.isNotEmpty())
                    IconButton(onClick = { query = "" }) { Icon(Icons.Filled.Clear, stringResource(R.string.clear)) }
            },
            singleLine = true,
            modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp),
        )
        if (groups.isNotEmpty()) {
            LazyRow(
                contentPadding = PaddingValues(horizontal = 16.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                item {
                    FilterChip(selected = group == null, onClick = { group = null },
                        label = { Text(stringResource(R.string.all_groups)) })
                }
                items(groups) { name ->
                    FilterChip(selected = group == name, onClick = { group = if (group == name) null else name },
                        label = { Text(name) })
                }
            }
        }
        Text(
            stringResource(R.string.count, visible.size, favorites.size),
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
        )
        LazyColumn(Modifier.fillMaxSize(), contentPadding = PaddingValues(bottom = 24.dp)) {
            items(visible, key = { System.identityHashCode(it) }) { favorite ->
                FavoriteRow(favorite) { selected = favorite }
                HorizontalDivider()
            }
        }
    }

    selected?.let { favorite ->
        FavoriteSheet(favorite, snackbar) { selected = null }
    }
}

@Composable
private fun FavoriteRow(favorite: Favorite, onClick: () -> Unit) {
    Row(
        Modifier.fillMaxWidth().clickable(onClick = onClick).padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        ProtocolBadge(favorite.protocol)
        Spacer(Modifier.size(12.dp))
        Column(Modifier.weight(1f)) {
            Text(favorite.name, style = MaterialTheme.typography.titleMedium, maxLines = 1, overflow = TextOverflow.Ellipsis)
            val details = listOf(favorite.address, favorite.groups.joinToString(", ")).filter { it.isNotEmpty() }
            Text(details.joinToString("  ·  "), style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1, overflow = TextOverflow.Ellipsis)
        }
    }
}

@Composable
private fun ProtocolBadge(protocol: String) {
    Surface(
        color = MaterialTheme.colorScheme.secondaryContainer,
        shape = MaterialTheme.shapes.small,
        modifier = Modifier.widthIn(min = 56.dp),
    ) {
        Text(protocol.take(6), style = MaterialTheme.typography.labelMedium, fontWeight = FontWeight.Bold,
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 6.dp))
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun FavoriteSheet(favorite: Favorite, snackbar: SnackbarHostState, onDismiss: () -> Unit) {
    val context = LocalContext.current
    val launcher = remember { ConnectionLauncher(context.applicationContext) }
    var showPassword by remember { mutableStateOf(false) }
    // the modal sheet is a separate window above the snackbar host, toast is visible above it
    val show = { id: Int -> Toast.makeText(context, id, Toast.LENGTH_SHORT).show() }

    ModalBottomSheet(onDismissRequest = onDismiss, sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)) {
        Column(
            Modifier.fillMaxWidth().verticalScroll(rememberScrollState()).padding(horizontal = 24.dp).navigationBarsPadding(),
        ) {
            Text(favorite.name, style = MaterialTheme.typography.headlineSmall)
            Spacer(Modifier.height(16.dp))
            Field(R.string.protocol, favorite.protocol)
            Field(R.string.server, favorite.address) { copy(context, launcher, R.string.server, favorite.address, false, show) }
            if (favorite.fullUserName.isNotEmpty())
                Field(R.string.user, favorite.fullUserName) {
                    copy(context, launcher, R.string.user, favorite.fullUserName, false, show)
                }
            if (favorite.password.isNotEmpty())
                Field(R.string.password, if (showPassword) favorite.password else "••••••••", monospace = showPassword) {
                    copy(context, launcher, R.string.password, favorite.password, true, show)
                }
            if (favorite.groups.isNotEmpty())
                Field(R.string.groups, favorite.groups.joinToString(", "))
            if (favorite.notes.isNotEmpty()) {
                Text(stringResource(R.string.notes), style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.primary, modifier = Modifier.padding(top = 8.dp))
                SelectionContainer { Text(favorite.notes, style = MaterialTheme.typography.bodyLarge) }
            }
            Spacer(Modifier.height(24.dp))

            Button(
                onClick = {
                    if (favorite.password.isNotEmpty() && favorite.protocol.uppercase() in setOf("RDP", "SSH", "TELNET", "VNC")) {
                        launcher.copy(context.getString(R.string.password), favorite.password, true)
                        show(R.string.password_copied_for_connect)
                    }
                    when (val result = launcher.connect(favorite)) {
                        ConnectionLauncher.Result.Started -> Unit
                        is ConnectionLauncher.Result.NoClient -> show(result.messageId)
                    }
                },
                enabled = launcher.canConnect(favorite),
                modifier = Modifier.fillMaxWidth(),
            ) {
                Text(if (launcher.canConnect(favorite)) stringResource(R.string.connect) else stringResource(R.string.protocol_not_supported))
            }
            if (favorite.password.isNotEmpty()) {
                Spacer(Modifier.height(8.dp))
                OutlinedButton(onClick = { showPassword = !showPassword }, modifier = Modifier.fillMaxWidth()) {
                    Text(stringResource(if (showPassword) R.string.hide_password else R.string.show_password))
                }
            }
            Spacer(Modifier.height(24.dp))
        }
    }
}

private fun copy(context: Context, launcher: ConnectionLauncher, labelId: Int, text: String, sensitive: Boolean,
                 show: (Int) -> Any) {
    launcher.copy(context.getString(labelId), text, sensitive)
    show(if (sensitive) R.string.password_copied else R.string.copied)
}

@Composable
private fun Field(labelId: Int, value: String, monospace: Boolean = false, onCopy: (() -> Unit)? = null) {
    Row(
        Modifier.fillMaxWidth().let { if (onCopy != null) it.clickable(onClick = onCopy) else it }.padding(vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.weight(1f)) {
            Text(stringResource(labelId), style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary)
            Text(value, style = MaterialTheme.typography.bodyLarge,
                fontFamily = if (monospace) FontFamily.Monospace else FontFamily.Default)
        }
        if (onCopy != null)
            Text(stringResource(R.string.copy), style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}
